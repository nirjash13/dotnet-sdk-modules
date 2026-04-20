# Engineers Handbook — Modular SaaS Chassis SDK

This guide is for engineers building modules on top of the Chassis SDK or extending the SDK itself. **This is a Phase-0 document** — most recipes below are forward-looking against the implementation plan; code examples are intent, not yet compilable.

For strategic context and decision rationale, read `docs/IMPLEMENTATION_PLAN.md` (architecture, phases, tradeoffs) and `.claude/memory/decisions.md` (Approach A+ rationale).

---

## Mental Model: 5 Core Concepts

### 1. Module
A **bounded context** with its own domain logic, database tables, and async event contracts. Structured as Clean Architecture:
- **Contracts** (netstandard2.0;net10.0) — commands, queries, integration events as records; consumed by other modules and external systems
- **Domain** (net10.0) — entities, value objects, domain events; *zero* infra dependencies
- **Application** (net10.0) — MassTransit consumers (handlers), FluentValidation validators, DTOs, Mapster profiles
- **Infrastructure** (net10.0) — `YourModuleDbContext : ChassisDbContext`, EF Core migrations, external API clients
- **Api** (net10.0) — `IModuleStartup` implementation wiring DI and HTTP endpoints under `/api/v1/yourmodule/*`

Modules are simultaneously projects in this monorepo (project references, sub-second builds) and NuGet packages (external consumption).

### 2. IModuleStartup
The **module bootstrap contract**. Every module's `Api` project implements this interface:

```csharp
public interface IModuleStartup
{
    string ModuleName { get; }
    void ConfigureServices(IServiceCollection services);
    void MapEndpoints(IEndpointRouteBuilder routes);
}
```

The Chassis.Host's `ModuleLoader` scans assemblies at startup, discovers all `IModuleStartup` implementations via reflection, invokes them in dependency order. Emits a `chassis_module_load_duration_seconds` meter per module.

### 3. IModuleDispatcher
The **unified dispatch abstraction** — sends commands and publishes events to handlers, indifferently of transport:

```csharp
public interface IModuleDispatcher
{
    Task<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request, 
        CancellationToken ct = default)
        where TRequest : ICommand;
    
    Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}
```

**In-process** (Phase 1+): MassTransit Mediator runs handlers on the current thread or MT's scheduler.
**Out-of-process** (Phase 4+): MassTransit Bus serializes to RabbitMQ; handlers run in isolated consumers.

The **transport toggle** is a single appsettings.json switch per edge:
```json
{
  "Dispatch": {
    "Ledger→Reporting": "inproc",  // or "bus"
    "Identity→Ledger": "bus"
  }
}
```
Handlers don't know or care. Same code path either way.

### 4. Tenant Context
**Ambient Context pattern** backed by `AsyncLocal<TenantContext>`. Flow:

```
HTTP request (JWT with tenant_id claim)
  → JwtBearer validates + extracts claims
  → TenantMiddleware reads tenant_id and populates AsyncLocal
  → Handler accesses via ITenantContextAccessor.Current
  → EF Core query filter auto-applies WHERE tenant_id = context.TenantId
  → TenantConnectionInterceptor issues SET LOCAL app.tenant_id = '<guid>'
  → Postgres RLS policy: USING (tenant_id = current_setting('app.tenant_id')::uuid)
```

**Defense in depth:** Cross-tenant reads return zero rows even if the EF filter is missing — both layers must fail for a breach.

MassTransit pipeline filter copies the context onto message headers so downstream consumers re-hydrate it identically in their own AsyncLocal.

### 5. Saga
A **long-running orchestrated workflow** coordinating commands across modules with automatic compensation on failure. Example (Phase 5):

```
AssociationRegistrationStarted
  → CreateUser (Identity module)
    ✓ → InitLedger (Ledger module)
      ✓ → ProvisionReporting (Reporting module)
        ✗ Timeout
      ← RollbackLedger (compensating)
    ← DeleteUser (compensating)
  ✗ Registration cancelled
```

Saga state persisted in a dedicated `registration` schema. Compensating commands declared per module. MT state machine (Automatonymous DSL) orchestrates the flow. Visible in Grafana `saga-health.json` dashboard.

---

## How to Add a New Module

### 1. Create the directory structure

```bash
mkdir -p src/Modules/YourModule/{YourModule.Contracts,YourModule.Domain,YourModule.Application,YourModule.Infrastructure,YourModule.Api}
cd src/Modules/YourModule
```

### 2. YourModule.Contracts (netstandard2.0;net10.0)

Contracts are immutable records — they cross wire boundaries and must be versionable.

**YourModule.Contracts.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Chassis.SharedKernel\Chassis.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

**Commands and queries:**
```csharp
namespace YourModule.Contracts.Commands;

public record CreateAccountCommand(
    Guid TenantId,
    string AccountName,
    AccountType Type) : ICommand;

public record GetAccountBalanceQuery(
    Guid TenantId,
    Guid AccountId) : IQuery<decimal>;
```

**Integration events** (published to other modules):
```csharp
namespace YourModule.Contracts.Events;

public record AccountCreated(
    Guid TenantId,
    Guid AccountId,
    string Name,
    DateTimeOffset CreatedAt) : IIntegrationEvent;
```

Do *not* reference EF Core types, validators, or infrastructure. Types must be serializable (use DateTimeOffset, not DateTime; Guid for identifiers).

### 3. YourModule.Domain (net10.0 — zero infra dependencies)

Entities enforce invariants; value objects are immutable; domain events capture what happened.

```csharp
namespace YourModule.Domain.Accounts;

public class Account : IEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public AccountType Type { get; private set; }
    public Money Balance { get; private set; }
    
    private readonly List<DomainEvent> _domainEvents = [];
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    private Account() { } // EF only
    
    public static Account Create(Guid tenantId, string name, AccountType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Account name cannot be empty");
        if (name.Length > 100)
            throw new DomainException("Account name exceeds 100 characters");
            
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Type = type,
            Balance = Money.Zero
        };
        
        account._domainEvents.Add(
            new AccountCreated(account.Id, account.TenantId, account.Name, DateTimeOffset.UtcNow));
        
        return account;
    }
    
    public void Post(Money amount)
    {
        if (amount <= Money.Zero)
            throw new DomainException("Cannot post zero or negative amount");
        Balance = Balance.Add(amount);
    }
}

public record DomainEvent(Guid Id, DateTimeOffset Timestamp, Guid TenantId)
{
    protected DomainEvent() : this(Guid.NewGuid(), DateTimeOffset.UtcNow, default!) { }
}

public record AccountCreated(Guid AccountId, Guid TenantId, string Name, DateTimeOffset At)
    : DomainEvent(Guid.NewGuid(), At, TenantId);
```

**Never reference:**
- EF Core (no `DbContext`, `DbSet`, `IAsyncEnumerable`)
- ASP.NET Core (no `HttpClient`, `IOptions`)
- MassTransit (no `IPublishEndpoint`)
- Validation (domain invariants live in constructors)

Verification: `NetArchTest` fails the build if Domain touches infra. See Phase 9.

### 4. YourModule.Application (net10.0)

Handlers are MassTransit consumers. Validators are FluentValidation rules. Mapster profiles wire DTOs.

**Handler (MassTransit consumer):**
```csharp
namespace YourModule.Application.Accounts.Create;

public class CreateAccountHandler : IConsumer<CreateAccountCommand>
{
    private readonly IAccountRepository _accounts;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITenantContextAccessor _tenantContext;
    
    public CreateAccountHandler(
        IAccountRepository accounts,
        IPublishEndpoint publishEndpoint,
        ITenantContextAccessor tenantContext)
    {
        _accounts = accounts;
        _publishEndpoint = publishEndpoint;
        _tenantContext = tenantContext;
    }
    
    public async Task Consume(ConsumeContext<CreateAccountCommand> context)
    {
        var command = context.Message;
        
        // Tenant is already verified by middleware; assert for safety
        if (_tenantContext.Current?.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Tenant mismatch");
        
        var account = Account.Create(command.TenantId, command.AccountName, command.Type);
        
        _accounts.Add(account);
        await _accounts.SaveChangesAsync(context.CancellationToken);
        
        // Publish integration event; MassTransit filter copies tenant context to headers
        await _publishEndpoint.Publish(
            new AccountCreated(
                account.Id,
                account.TenantId,
                account.Name,
                DateTimeOffset.UtcNow),
            context.CancellationToken);
    }
}
```

**Validator:**
```csharp
namespace YourModule.Application.Accounts.Create;

public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.AccountName)
            .NotEmpty().WithMessage("Account name is required")
            .MaximumLength(100).WithMessage("Account name must not exceed 100 characters");
        
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid account type");
    }
}
```

**Query handler:**
```csharp
namespace YourModule.Application.Accounts.Get;

public class GetAccountBalanceHandler : IConsumer<GetAccountBalanceQuery>
{
    private readonly IAccountRepository _accounts;
    
    public GetAccountBalanceHandler(IAccountRepository accounts) => _accounts = accounts;
    
    public async Task Consume(ConsumeContext<GetAccountBalanceQuery> context)
    {
        var query = context.Message;
        
        var account = await _accounts.GetByIdAsync(query.AccountId, context.CancellationToken);
        
        if (account is null)
        {
            await context.RespondAsync<QueryNotFound>(new() { Message = "Account not found" });
            return;
        }
        
        await context.RespondAsync(account.Balance.Amount);
    }
}
```

**DTOs:**
```csharp
namespace YourModule.Application.Accounts;

public record AccountDto(Guid Id, string Name, AccountType Type, decimal BalanceAmount);
```

**Mapster profile:**
```csharp
namespace YourModule.Application.Accounts;

public class AccountMappingProfile : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Account, AccountDto>()
            .Map(dest => dest.BalanceAmount, src => src.Balance.Amount);
    }
}
```

Register in `IModuleStartup`:
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IValidator<CreateAccountCommand>, CreateAccountCommandValidator>();
    services.AddMassTransitConsumer<CreateAccountHandler>();
    services.AddMassTransitConsumer<GetAccountBalanceHandler>();
    
    TypeAdapterConfig.GlobalSettings.Scan(typeof(CreateAccountHandler).Assembly);
}
```

### 5. YourModule.Infrastructure (net10.0)

DbContext, migrations, repositories, external clients.

**DbContext:**
```csharp
namespace YourModule.Infrastructure.Persistence;

public class YourModuleDbContext : ChassisDbContext
{
    public DbSet<Account> Accounts => Set<Account>();
    
    public YourModuleDbContext(DbContextOptions<YourModuleDbContext> options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Account>(b =>
        {
            b.ToTable("accounts", "yourmodule");
            b.HasKey(x => x.Id);
            
            b.Property(x => x.TenantId)
                .IsRequired();
            b.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();
            b.Property(x => x.Type)
                .HasConversion<string>()
                .IsRequired();
            
            // ChassisDbContext auto-applies global query filter for TenantId
            b.HasQueryFilter(x => x.TenantId == EF.Property<Guid>("__tenant_id__"));
            
            b.HasIndex(x => new { x.TenantId, x.Name })
                .IsUnique();
        });
    }
}
```

**Repository:**
```csharp
namespace YourModule.Infrastructure.Repositories;

public interface IAccountRepository
{
    void Add(Account account);
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public class AccountRepository : IAccountRepository
{
    private readonly YourModuleDbContext _context;
    
    public AccountRepository(YourModuleDbContext context) => _context = context;
    
    public void Add(Account account) => _context.Accounts.Add(account);
    
    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    
    public async Task SaveChangesAsync(CancellationToken ct) =>
        await _context.SaveChangesAsync(ct);
}
```

### 6. YourModule.Api (net10.0)

Implements `IModuleStartup`. Wires DI. Maps HTTP endpoints.

```csharp
namespace YourModule.Api;

public class YourModuleStartup : IModuleStartup
{
    public string ModuleName => "YourModule";
    
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<YourModuleDbContext>(opts =>
            opts.UseNpgsql(services.BuildServiceProvider()
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("DefaultConnection")));
        
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IValidator<CreateAccountCommand>, CreateAccountCommandValidator>();
        services.AddMassTransitConsumer<CreateAccountHandler>();
        
        TypeAdapterConfig.GlobalSettings.Scan(typeof(YourModuleStartup).Assembly);
    }
    
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/yourmodule")
            .WithOpenApi()
            .RequireAuthorization();
        
        group.MapPost("/accounts", CreateAccountAsync)
            .Produces<AccountDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .WithName("CreateAccount")
            .WithSummary("Create a new account");
        
        group.MapGet("/accounts/{id}", GetAccountAsync)
            .Produces<AccountDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetAccount");
    }
    
    private static async Task<IResult> CreateAccountAsync(
        CreateAccountRequest request,
        IModuleDispatcher dispatcher,
        ITenantContextAccessor tenantContext,
        CancellationToken ct)
    {
        var tenantId = tenantContext.Current?.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context");
        
        var command = new CreateAccountCommand(tenantId, request.Name, request.Type);
        
        // Validation via MT pipeline filter
        await dispatcher.SendAsync<CreateAccountCommand, Unit>(command, ct);
        
        return Results.Created($"/api/v1/yourmodule/accounts/{command.Id}", null);
    }
    
    private static async Task<IResult> GetAccountAsync(
        Guid id,
        IModuleDispatcher dispatcher,
        CancellationToken ct)
    {
        var query = new GetAccountBalanceQuery(Guid.Empty, id);
        // Note: query handler returns Account DTO via Mapster
        var result = await dispatcher.SendAsync<GetAccountBalanceQuery, AccountDto?>(query, ct);
        
        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }
}

public record CreateAccountRequest(string Name, AccountType Type);
```

### 7. Add to solution and migrations

**Add projects to Chassis.sln:**
```bash
dotnet sln Chassis.sln add src/Modules/YourModule/*/*.csproj
```

**Create migrations:**
```bash
mkdir -p migrations/yourmodule
cd migrations/yourmodule
```

Create `V1__create_tables.sql`:
```sql
CREATE SCHEMA yourmodule;

CREATE TABLE yourmodule.accounts (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    type VARCHAR(50) NOT NULL,
    balance_amount NUMERIC(19,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_accounts_tenant_id ON yourmodule.accounts (tenant_id);
CREATE UNIQUE INDEX idx_accounts_tenant_name ON yourmodule.accounts (tenant_id, name);
```

Create `V2__rls_policies.sql` using the template from `migrations/_template/rls-policy.sql`:
```sql
ALTER TABLE yourmodule.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE yourmodule.accounts FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON yourmodule.accounts
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
```

### 8. Add NetArchTest rule

In `tests/Chassis.ArchitectureTests/YourModuleArchTests.cs`:

```csharp
[Fact]
public void YourModuleDomain_ShouldNotReferencePlatformLibraries()
{
    var result = Types
        .InNamespace("YourModule.Domain")
        .Should()
        .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
        .And.NotHaveDependencyOn("MassTransit")
        .And.NotHaveDependencyOn("Microsoft.AspNetCore")
        .Check(Architecture);
    
    result.IsSuccessful.Should().BeTrue();
}
```

### 9. Test locally

```bash
dotnet build -warnaserror
dotnet test
```

---

## How to Add a Command Handler

Recipe: MassTransit consumer (IConsumer<TCommand>), validator, registration.

**Example: Post a ledger transaction.**

1. **Define the command** in `Ledger.Contracts/Commands/PostTransactionCommand.cs`:
```csharp
public record PostTransactionCommand(
    Guid TenantId,
    Guid AccountId,
    decimal Amount,
    string Description) : ICommand;
```

2. **Write the validator** in `Ledger.Application/PostTransaction/PostTransactionCommandValidator.cs`:
```csharp
public class PostTransactionCommandValidator : AbstractValidator<PostTransactionCommand>
{
    public PostTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be positive");
        
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(255);
    }
}
```

3. **Implement the handler** in `Ledger.Application/PostTransaction/PostTransactionHandler.cs`:
```csharp
public class PostTransactionHandler : IConsumer<PostTransactionCommand>
{
    private readonly ILedgerRepository _ledger;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IPublishEndpoint _publishEndpoint;
    
    public PostTransactionHandler(
        ILedgerRepository ledger,
        ITenantContextAccessor tenantContext,
        IPublishEndpoint publishEndpoint)
    {
        _ledger = ledger;
        _tenantContext = tenantContext;
        _publishEndpoint = publishEndpoint;
    }
    
    public async Task Consume(ConsumeContext<PostTransactionCommand> context)
    {
        var command = context.Message;
        
        if (_tenantContext.Current?.TenantId != command.TenantId)
            throw new UnauthorizedAccessException("Tenant mismatch");
        
        var account = await _ledger.GetAccountAsync(command.AccountId, context.CancellationToken);
        if (account is null)
        {
            await context.RespondAsync<CommandNotFound>(new() { Message = "Account not found" });
            return;
        }
        
        var posting = Posting.Create(account.Id, command.Amount, command.Description);
        account.Post(posting);
        
        _ledger.UpdateAccount(account);
        await _ledger.SaveChangesAsync(context.CancellationToken);
        
        await _publishEndpoint.Publish(
            new TransactionPosted(
                account.TenantId,
                posting.Id,
                account.Id,
                command.Amount,
                DateTimeOffset.UtcNow),
            context.CancellationToken);
        
        await context.RespondAsync(new PostTransactionResponse(posting.Id));
    }
}
```

4. **Register in IModuleStartup** (Ledger.Api):
```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddScoped<IValidator<PostTransactionCommand>, PostTransactionCommandValidator>();
    services.AddMassTransitConsumer<PostTransactionHandler>();
    // ... other registrations
}
```

5. **Map the endpoint** (Ledger.Api):
```csharp
public void MapEndpoints(IEndpointRouteBuilder routes)
{
    routes.MapGroup("/api/v1/ledger")
        .RequireAuthorization()
        .MapPost("/transactions", PostTransactionAsync)
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
}

private static async Task<IResult> PostTransactionAsync(
    PostTransactionRequest request,
    IModuleDispatcher dispatcher,
    ITenantContextAccessor tenantContext,
    CancellationToken ct)
{
    var tenantId = tenantContext.Current?.TenantId
        ?? throw new UnauthorizedAccessException();
    
    var command = new PostTransactionCommand(
        tenantId,
        request.AccountId,
        request.Amount,
        request.Description);
    
    await dispatcher.SendAsync<PostTransactionCommand, Unit>(command, ct);
    
    return Results.NoContent();
}
```

The validator runs automatically via MassTransit's pipeline filter. If validation fails, the framework returns a 400 ProblemDetails response.

---

## How to Add a Query Handler

Same pattern: `IConsumer<TQuery>`, validator (optional for queries), registration.

**Example: Get account balance.**

```csharp
public class GetAccountBalanceHandler : IConsumer<GetAccountBalanceQuery>
{
    private readonly LedgerDbContext _context;
    private readonly ITenantContextAccessor _tenantContext;
    
    public GetAccountBalanceHandler(
        LedgerDbContext context,
        ITenantContextAccessor tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }
    
    public async Task Consume(ConsumeContext<GetAccountBalanceQuery> context)
    {
        var query = context.Message;
        
        var account = await _context.Accounts
            .AsNoTracking()  // ← Essential for read-only queries
            .Where(a => a.Id == query.AccountId)
            .Select(a => new { a.Id, a.Name, a.Balance })
            .FirstOrDefaultAsync(context.CancellationToken);
        
        if (account is null)
        {
            await context.RespondAsync<QueryNotFound>(new());
            return;
        }
        
        await context.RespondAsync(new GetAccountBalanceResponse(account.Balance));
    }
}
```

**Key rule:** Always use `AsNoTracking()` for read-only queries. Never return EF entities directly — project to DTOs with `.Select()`.

---

## Tenancy Cookbook

### Understanding the flow

When a request arrives with a JWT:
1. `JwtBearer` middleware validates and extracts claims
2. `TenantMiddleware` reads `tenant_id` claim and calls `TenantContextAccessor.Current = new TenantContext(...)`
3. AsyncLocal now holds the tenant context for this async call tree
4. Handler accesses context via `ITenantContextAccessor.Current`
5. EF Core global query filter auto-applies `WHERE tenant_id = <from-context>`
6. Before executing SQL, `TenantConnectionInterceptor` issues `SET LOCAL app.tenant_id = '<guid>'`
7. Postgres RLS policy evaluates: `USING (tenant_id = current_setting('app.tenant_id')::uuid)`
8. Result: double filtering — cross-tenant reads return zero rows even if one layer fails

### Do's and Don'ts

**Do:**
- Read tenant context in handlers: `var tenantId = _tenantContext.Current?.TenantId;`
- Assert tenant match on command input: `if (command.TenantId != tenantId) throw ...`
- Pass tenant_id explicitly in domain entities and integration events
- Use `AsNoTracking()` in queries that touch multi-tenant data

**Don't:**
- Cache tenant-scoped data in `services.AddSingleton<>` — each request gets its own AsyncLocal
- Use `Task.Run` inside handlers (breaks AsyncLocal flow) — if truly needed, use `Task.Factory.StartNew` with `TaskScheduler.Default`
- Omit the EF Core global query filter — RLS is the second line of defense, not the first
- Hardcode tenant_id in middleware or filters — it propagates via MT headers automatically

### Adding RLS to a new table

1. Create the table with a `tenant_id UUID NOT NULL` column:
```sql
CREATE TABLE mymodule.mytable (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    ...
);
```

2. Add a migration file `V#__rls_policies.sql`:
```sql
ALTER TABLE mymodule.mytable ENABLE ROW LEVEL SECURITY;
ALTER TABLE mymodule.mytable FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON mymodule.mytable
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
```

3. Register the table with the chassis in `YourModuleDbContext.OnModelCreating`:
```csharp
modelBuilder.Entity<MyEntity>(b =>
{
    // ... configure properties
    b.HasQueryFilter(x => x.TenantId == EF.Property<Guid>("__tenant_id__"));
});
```

4. NetArchTest will fail the build if you forget — see Phase 9.

### Testing tenant boundaries

Integration test template using `WebApplicationFactory` + `Testcontainers`:

```csharp
public class TenantIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    
    public TenantIsolationTests(CustomWebApplicationFactory factory) => _factory = factory;
    
    [Fact]
    public async Task TenantA_CannotReadTenantB_Data()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        var accountA = await CreateAccountAsync(tenantA, "Account A");
        var accountB = await CreateAccountAsync(tenantB, "Account B");
        
        var clientA = _factory.CreateAuthenticatedClient(tenantA);
        
        // Act
        var response = await clientA.GetAsync($"/api/v1/ledger/accounts/{accountB.Id}");
        
        // Assert — should be 404 (not found), not 403 (forbidden)
        // Proves IDOR resistance: tenant A doesn't even know the resource exists
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

---

## Switching a Module Edge from In-Proc to Bus

**One change. That's it.**

In `appsettings.json` or environment variables:
```json
{
  "Dispatch": {
    "Ledger→Reporting": "inproc"  // ← Change to "bus"
  }
}
```

MassTransit will:
- In-process: run the consumer on the current thread
- Out-of-process: serialize to RabbitMQ, deserialize in a consumer, same handler code

No business logic changes. Same validator pipeline. Same domain events. Same error handling.

This is the core feature of Approach A+ — the abstraction isolates the transport detail.

---

## Sagas: When and How

### When to use a saga

**Use a saga when:**
- A workflow spans multiple modules
- Compensation (rollback) is needed if any step fails
- You want to orchestrate the order and retry policy

**Example: Association registration (Phase 5) →**
```
1. CreateUser (Identity)
2. InitLedger (Ledger)
3. ProvisionReporting (Reporting)
  (any fail → compensate in reverse)
```

**Don't use a saga for:**
- A single module's internal workflow (use domain events + async handlers)
- Fire-and-forget pub/sub (use `IPublishEndpoint`)

### Anatomy of a saga (MassTransit + Automatonymous)

```csharp
namespace Registration.Application.AssociationRegistration;

public class AssociationRegistrationSaga :
    SagaStateMachine<AssociationRegistrationState>
{
    public State Started { get; set; }
    public State UserCreated { get; set; }
    public State LedgerInitialized { get; set; }
    public State ReportingProvisioned { get; set; }
    public State Completed { get; set; }
    public State Faulted { get; set; }
    
    public Event<AssociationRegistrationStarted> StartRegistration { get; set; }
    public Event<UserCreatedEvent> UserCreated_Event { get; set; }
    public Event<LedgerInitializedEvent> LedgerInitialized_Event { get; set; }
    public Event<ReportingProvisionedEvent> ReportingProvisioned_Event { get; set; }
    public Event<SagaTimeoutExpired> Timeout { get; set; }
    
    public AssociationRegistrationSaga()
    {
        // Define state transitions
        Initially(
            When(StartRegistration)
                .Then(ctx => {
                    ctx.Saga.CorrelationId = ctx.Message.CorrelationId;
                    ctx.Saga.TenantId = ctx.Message.TenantId;
                })
                .PublishAsync(ctx => ctx.Init<CreateUserCommand>(
                    new() {
                        TenantId = ctx.Message.TenantId,
                        Email = ctx.Message.Email
                    }))
                .TransitionTo(Started));
        
        During(Started,
            When(UserCreated_Event)
                .Then(ctx => ctx.Saga.UserId = ctx.Message.UserId)
                .PublishAsync(ctx => ctx.Init<InitLedgerCommand>(
                    new() {
                        TenantId = ctx.Message.TenantId,
                        UserId = ctx.Message.UserId
                    }))
                .TransitionTo(UserCreated));
        
        During(UserCreated,
            When(LedgerInitialized_Event)
                .PublishAsync(ctx => ctx.Init<ProvisionReportingCommand>(
                    new() {
                        TenantId = ctx.Message.TenantId,
                        UserId = ctx.Message.UserId
                    }))
                .TransitionTo(LedgerInitialized));
        
        During(LedgerInitialized,
            When(ReportingProvisioned_Event)
                .TransitionTo(Completed),
            When(Timeout)
                .Then(ctx => {
                    // Compensate in reverse
                })
                .PublishAsync(ctx => ctx.Init<UnprovisionReportingCommand>(...))
                .TransitionTo(Faulted));
    }
}

public class AssociationRegistrationState : SagaStateMachineInstance, IVersionedSagaState
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public int Version { get; set; }
}
```

Saga state persisted in `registration` schema. Each step is a compensating command in reverse. Visible in Grafana.

---

## Testing Policy

See `.claude/rules/testing.md` for the complete policy. Summary:

**Load-bearing tests only.** A test earns its place if:
1. It would fail when the feature is realistically broken
2. A user or downstream service would notice
3. No other test catches the same bug
4. It's not testing the framework, it's testing your code

**Test budget per change type:**
| Change | Target | Ceiling |
|--------|--------|---------|
| New command handler | 1 happy + 1 validation + 1 not-found | ≤5 |
| New API endpoint | 1 integration (happy) + 1 auth + 1 validation | ≤5 |
| Bug fix | 1 regression test | 1 |
| Refactor (existing coverage) | 0 new tests | 0 |

**Integration tests** (highest value): `WebApplicationFactory` + `Testcontainers`
```csharp
[Fact]
public async Task PostTransaction_WithValidRequest_Returns204()
{
    var client = _factory.CreateAuthenticatedClient(tenantId);
    var response = await client.PostAsJsonAsync("/api/v1/ledger/transactions", 
        new { AccountId = accountId, Amount = 100, Description = "test" });
    
    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
}
```

**Handler unit tests** (medium value): Mocked repositories
**Domain tests** (targeted): Entity invariants only

Banned patterns: FluentValidation echo tests, EF Core plumbing tests, over-mocked unit tests, happy-path duplication.

---

## Load Testing

### Run locally

```bash
docker-compose -f docker-compose.yml up -d  # Start Postgres + RabbitMQ + OTel stack

# Run a steady-state scenario
k6 run loadtests/k6/scenarios/steady.js \
  --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
  --tag test_run_id=$(uuidgen)

# Open Grafana and view `loadtest-results.json` dashboard
open http://localhost:3000
```

### SLO targets (v1)

| Metric | Target |
|--------|--------|
| POST /api/v1/ledger/transactions p95 | < 150 ms |
| GET /api/v1/ledger/accounts/{id} p95 | < 80 ms |
| `/connect/token` p95 | < 250 ms |
| Saga completion p99 | < 2 s |
| Outbox lag p99 | < 500 ms |
| Error rate (5xx) | < 0.5% |

See `docs/IMPLEMENTATION_PLAN.md` §7.4 for full table and chaos overlays.

---

## Observability

### Key metrics to watch

| Meter | What it means | Alert threshold |
|-------|---------------|-----------------|
| `chassis_commands_duration_seconds{module,command}` | Handler latency | p95 > 200 ms |
| `chassis_outbox_lag_seconds{module}` | Time from DB commit to bus delivery | p99 > 500 ms |
| `chassis_outbox_depth{module}` | Undelivered messages in outbox table | > 1000 |
| `chassis_rls_denials_total{module,table}` | RLS policy rejections | > 0 (investigate) |
| `chassis_saga_active_count{saga}` | In-flight sagas | > 1000 (saturation) |
| `chassis_module_load_duration_seconds{module}` | Startup time per module | > 5 s (slow loader) |

### Dashboards

Grafana dashboards are JSON-provisioned under `deploy/grafana/provisioning/dashboards/`:
- `chassis-overview.json` — health traffic light per module
- `module-latency.json` — per-module p50/p95/p99
- `saga-health.json` — saga states, time-in-state, failures
- `rls-denial-rate.json` — multi-tenant isolation health
- `outbox-lag.json` — delivery SLO tracking

### Adding a new meter to a module

In your handler, inject `IMetricsCollector` (or use `System.Diagnostics.Metrics.Meter` directly):

```csharp
private static readonly Meter _meter = new("YourModule.Application", "1.0.0");
private static readonly Counter<long> _transactionsProcessed = 
    _meter.CreateCounter<long>("ledger.transactions.processed");

public async Task Consume(ConsumeContext<PostTransactionCommand> context)
{
    // ... business logic
    
    _transactionsProcessed.Add(1, new KeyValuePair<string, object?>(
        "tenant_id", context.Message.TenantId));
}
```

Register the meter in `Program.cs`:
```csharp
services.AddOpenTelemetry()
    .WithMetrics(b => b
        .AddMeter("YourModule.Application"));
```

---

## Publishing a New Module Version

### Local iteration

During development, use project references:
```xml
<ProjectReference Include="..\Ledger.Api\Ledger.Api.csproj" />
```

Builds in sub-second increments.

### CI pack

On every push, CI runs:
```bash
dotnet pack src/Modules/YourModule/YourModule.Api \
  --configuration Release \
  --output ./nupkgs
```

MinVer derives the version from the nearest git tag + commit distance.

### Publish to NuGet feed

On main-branch merge:
```bash
dotnet nuget push ./nupkgs/*.nupkg \
  --source $NUGET_FEED_URL \
  --api-key $NUGET_API_KEY
```

### SemVer discipline

- **Patch** (1.0.1): internal refactors, bug fixes, non-breaking domain changes
- **Minor** (1.1.0): new handlers, new RPC methods, additive DTOs
- **Major** (2.0.0): wire contract breaking changes, command/event renames, parameter drops

Contracts have **stricter** compat rules than internal code. Any breaking wire change is a major bump.

Tag with:
```bash
git tag v1.2.0
git push --tags
```

---

## Debugging Common Issues

### Cross-tenant data visible

**Symptoms:** Tenant A can read Tenant B's rows.

**Diagnosis:**
1. Verify `TenantConnectionInterceptor` fired: check `SET LOCAL app.tenant_id` in Postgres logs (`log_statement=all`)
2. Verify the entity has `ITenantScoped` marker or `HasQueryFilter` in EF
3. Verify RLS policy exists: `\dp myschema.mytable` in psql

**Fix:**
- Add global query filter to the entity in `DbContext.OnModelCreating`
- Create the RLS policy in a new migration
- Nightly cross-tenant smoke test will fail CI until fixed

### Outbox lag growing

**Symptoms:** `chassis_outbox_lag_seconds` p99 > 500 ms and growing.

**Diagnosis:**
1. Is RabbitMQ alive? `rabbitmqctl ping`
2. Is the consumer running? Check `rabbitmqctl list_consumers`
3. Is the outbox table locked? `SELECT * FROM pg_locks WHERE relation = (SELECT oid FROM pg_class WHERE relname = 'outbox')`
4. What's in the DLQ? Check `Ledger.TransactionPosted.DLQ` queue

**Fix:**
- Restart the delivery service: `dotnet run --project src/Chassis.Host`
- Increase `PrefetchCount` in appsettings if consumer is starved
- Check Grafana `outbox-lag.json` dashboard

### JWT rejected

**Symptoms:** `401 Unauthorized` on every endpoint.

**Diagnosis:**
1. Verify Authority matches (IdP base URL): `curl https://your-auth-server/.well-known/openid-configuration`
2. Verify token's `aud` claim matches configured Audience in `AddJwtBearer`
3. Verify token's `iss` claim matches Authority
4. Verify clock skew is < 5 minutes: `date; curl -I https://your-auth-server` and compare

**Fix:**
- Update `Audience` in appsettings
- Update Authority URL
- Sync server clocks
- Check ProblemDetails response body for the specific failure reason

### Saga stuck

**Symptoms:** Saga is in `UserCreated` state for hours; no progress.

**Diagnosis:**
1. Query saga state: `SELECT * FROM registration.association_registrations WHERE correlation_id = '<id>'`
2. Is the next event published? Check RabbitMQ queue depth
3. Is the consumer crashed? Check logs
4. Did it timeout? Check `Timeout` event in saga

**Fix:**
- Replay the next command: publish `InitLedgerCommand` with the correct saga CorrelationId
- Check Grafana `saga-health.json` for failure counts
- Inspect saga-health logs for the stuck saga's correlation_id

### NetArchTest failing on a new module

**Symptoms:** Build fails: "YourModule.Domain must not reference MassTransit"

**Diagnosis:** Domain layer has a using statement for `MassTransit`, EF Core, or another infra library.

**Fix:** Move the problematic code to Application or Infrastructure layer. Domain is intentionally isolated.

---

## Keeping This Handbook Up to Date

When architecture changes, update this document:

- **§2 Mental model** — when `IModuleStartup`, `IModuleDispatcher`, or tenancy flow changes
- **§3 Module recipe** — when Clean Architecture shape or scaffolding changes
- **§4/5 Command/query handlers** — when MassTransit consumer API or validation pattern changes
- **§6 Tenancy** — when context propagation or RLS policy template changes
- **§10 Observability** — when new chassis meters are added
- **§11 Publishing** — when NuGet feed or versioning strategy changes

Suggested cadence: review this handbook at each phase completion. If drift is detected, open a task.

---

## Further Reading

- **`README.md`** (root) — project elevator pitch and quickstart
- **`docs/IMPLEMENTATION_PLAN.md`** — full architecture, phases, decisions, load-testing strategy, security posture (local repo only, not pushed)
- **`.claude/memory/decisions.md`** — Approach A+ rationale and consequences
- **`.claude/CLAUDE.md`** — .NET standards, language rules, required project settings
- **`.claude/rules/backend.md`** — layer boundaries, EF Core rules, validation, UX requirements
- **`.claude/rules/testing.md`** — test policy, load-bearing filter, test budget per change type
- **MassTransit documentation** — consumer contracts, pipeline filters, saga state machines
- **Entity Framework Core documentation** — global query filters, interceptors, migrations
- **OpenIddict documentation** — OIDC flows, token validation, claim customization
- **OpenTelemetry .NET documentation** — instrumentation, exporters, custom meters

---

<!-- written-by: writer-haiku | model: haiku -->
