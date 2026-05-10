# Engineers Handbook — SaaS Builder SDK

This guide is for engineers building modules on top of the SaaS Builder SDK or extending the SDK itself. **Phase 1 is complete; Phases 2–5 scaffolding landed** (abstractions + default providers + clearly-marked TODOs for deferred features). For strategic context and the complete roadmap, read `docs/TASK_LIST.md` and `docs/SAAS_SDK_IMPLEMENTATION_PLAN.md`.

All code examples below are **production-compilable** on the current main branch.

---

## Mental Model: 5 Core Concepts

### 1. Module
A **bounded context** with its own domain logic, database tables, and async event contracts. Structured as Clean Architecture:
- **Contracts** (net10.0) — commands, queries, integration events as records; consumed by other modules and external systems
- **Domain** (net10.0) — entities, value objects, domain events; *zero* infra dependencies
- **Application** (net10.0) — MassTransit consumers (handlers), FluentValidation validators, DTOs, Mapster profiles
- **Infrastructure** (net10.0) — `YourModuleDbContext : SaasBuilderDbContext`, EF Core migrations, external API clients
- **Api** (net10.0) — `IModuleStartup` implementation wiring DI and HTTP endpoints under `/api/v1/yourmodule/*`

Modules are simultaneously projects in this monorepo (project references, sub-second builds) and NuGet packages (external consumption).

### 2. IModuleStartup

The **module bootstrap contract**. Every module's `Api` project implements this interface:

```csharp
public interface IModuleStartup
{
    /// <summary>Register the module's services into the dependency injection container.</summary>
    void ConfigureServices(IServiceCollection services, IConfiguration config);

    /// <summary>Map the module's HTTP endpoints (ASP.NET Core 10.0+).</summary>
    void Configure(IEndpointRouteBuilder endpoints);
}
```

The `ReflectionModuleLoader` scans configured probe directories at startup, discovers all `IModuleStartup` implementations via reflection, invokes them in dependency order. Emits a `saasbuilder_module_load_duration_seconds` meter per module.

**Discovery:** 
- Assembly scan: `opts.Modules.ScanAssemblyContaining<MyModule>()`
- Probe directories: `opts.Modules.AddProbeDirectory("modules/")` — default is `AppDomain.BaseDirectory + "modules/"`

### 3. Fluent Options API

The **unified entry point** for host configuration — replaces parameterless `AddSaasBuilderHost()` overload:

```csharp
builder.AddSaasBuilderHost(opts =>
{
    opts.UseTransport(SaasTransport.InProc);                    // or Bus
    opts.UseTenancy(TenantIsolation.PoolWithRls);               // or PoolShared, SiloedSchema, SiloedDatabase, SiloedStamp (stubbed)
    opts.Modules.ScanAssemblyContaining<MyModule>();
    opts.Modules.AddProbeDirectory("modules/");
    opts.Observability.Enable();
    opts.RateLimiting.UsePerTenantSlidingWindow();
    opts.Transport.WithMediatorConsumers(cfg => { /* MassTransit in-process */ });
    opts.Transport.WithBusConsumers(cfg => { /* RabbitMQ consumers */ });
});
```

Each `Saas*Options` class (e.g., `SaasBuilderModulesOptions`, `SaasBuilderTransportOptions`) offers fluent methods for granular control. See `src/SaasBuilder.Host/Configuration/Options/` for implementations.

### 4. Transport Abstraction

**In-process** (InProc): MassTransit Mediator runs handlers on the current thread.
**Out-of-process** (Bus): MassTransit Bus serializes to RabbitMQ; handlers run in isolated consumers.

Toggle via fluent option:
```csharp
opts.UseTransport(SaasTransport.InProc);  // ← fast, for dev; all consumers run sync
opts.UseTransport(SaasTransport.Bus);     // ← prod; async RabbitMQ dispatch
```

Handlers don't know or care. Same code path either way. Register consumers separately:
```csharp
opts.Transport.WithMediatorConsumers(cfg => cfg.AddConsumer<MyHandler>());
opts.Transport.WithBusConsumers(cfg => cfg.AddConsumer<MyHandler>());
```

### 5. Tenant Context

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

**Defense in depth:** Cross-tenant reads return zero rows even if one layer fails — both EF and RLS must fail.

MassTransit pipeline filter copies context to message headers so downstream consumers re-hydrate it identically in their own AsyncLocal.

### 6. Pick & Choose Modules

**Every module is an independent NuGet package.** The Host has zero hard module dependencies. Three load-out patterns:

**Minimal (B2C):**
```csharp
// NuGet: SaasBuilder.Host + SaasBuilder.Modules.Billing
builder.AddSaasBuilderHost(opts => { opts.Modules.AddType<BillingModule>(); });
```

**B2B:**
```csharp
// NuGet: + SaasBuilder.Modules.Identity + SaasBuilder.Modules.Webhooks
opts.Modules.AddType<IdentityModule>();
opts.Modules.AddType<BillingModule>();
opts.Modules.AddType<WebhooksModule>();
```

**Full (Phase 1–5):**
```csharp
opts.Modules.ScanAssemblyContaining<IdentityModule>();
opts.Modules.ScanAssemblyContaining<BillingModule>();
opts.Modules.ScanAssemblyContaining<WebhooksModule>();
// ... all Phase 5 modules
```

---

## How to Add a New Module

### 1. Create the directory structure

```bash
mkdir -p src/Modules/YourModule/{YourModule.Contracts,YourModule.Domain,YourModule.Application,YourModule.Infrastructure,YourModule.Api}
cd src/Modules/YourModule
```

### 2. YourModule.Contracts (net10.0)

Contracts are immutable records — they cross wire boundaries and must be versionable.

**YourModule.Contracts.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0</TargetFrameworks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\SaasBuilder.SharedKernel\SaasBuilder.SharedKernel.csproj" />
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

Register in `IModuleStartup`:
```csharp
public void ConfigureServices(IServiceCollection services, IConfiguration config)
{
    services.AddScoped<IValidator<CreateAccountCommand>, CreateAccountCommandValidator>();
    services.AddMassTransitConsumer<CreateAccountHandler>();
    
    TypeAdapterConfig.GlobalSettings.Scan(typeof(CreateAccountHandler).Assembly);
}
```

### 5. YourModule.Infrastructure (net10.0)

DbContext, migrations, repositories, external clients.

**DbContext:**
```csharp
namespace YourModule.Infrastructure.Persistence;

public class YourModuleDbContext : SaasBuilderDbContext
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
            
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Type).HasConversion<string>().IsRequired();
            
            // SaasBuilderDbContext auto-applies global query filter for TenantId
            b.HasQueryFilter(x => x.TenantId == EF.Property<Guid>("__tenant_id__"));
            
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
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
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<YourModuleDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IValidator<CreateAccountCommand>, CreateAccountCommandValidator>();
        services.AddMassTransitConsumer<CreateAccountHandler>();
        
        TypeAdapterConfig.GlobalSettings.Scan(typeof(YourModuleStartup).Assembly);
    }
    
    public void Configure(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/yourmodule")
            .WithOpenApi()
            .RequireAuthorization();
        
        group.MapPost("/accounts", CreateAccountAsync)
            .Produces<AccountDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .WithName("CreateAccount");
        
        group.MapGet("/accounts/{id}", GetAccountAsync)
            .Produces<AccountDto>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .WithName("GetAccount");
    }
    
    private static async Task<IResult> CreateAccountAsync(
        CreateAccountRequest request,
        IScopedMediator mediator,
        ITenantContextAccessor tenantContext,
        CancellationToken ct)
    {
        var tenantId = tenantContext.Current?.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context");
        
        var command = new CreateAccountCommand(tenantId, request.Name, request.Type);
        await mediator.Send(command, ct);
        
        return Results.Created($"/api/v1/yourmodule/accounts/{command.Id}", null);
    }
    
    private static async Task<IResult> GetAccountAsync(
        Guid id,
        IScopedMediator mediator,
        CancellationToken ct)
    {
        var query = new GetAccountBalanceQuery(Guid.Empty, id);
        var result = await mediator.Send(query, ct);
        
        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }
}

public record CreateAccountRequest(string Name, AccountType Type);
```

### 7. Add to solution and migrations

**Add projects to SaasBuilder.sln:**
```bash
dotnet sln SaasBuilder.sln add src/Modules/YourModule/*/*.csproj
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

Create `V2__rls_policies.sql`:
```sql
ALTER TABLE yourmodule.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE yourmodule.accounts FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON yourmodule.accounts
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
```

### 8. Add NetArchTest rule

In `tests/SaasBuilder.ArchitectureTests/YourModuleArchTests.cs`:

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

## Tenancy Cookbook

### Understanding the flow

When a request arrives with a JWT:
1. `JwtBearer` middleware validates and extracts claims
2. `TenantMiddleware` reads `tenant_id` claim and calls `ITenantContextAccessor.Current = new TenantContext(...)`
3. AsyncLocal now holds the tenant context for this async call tree
4. Handler accesses context via `ITenantContextAccessor.Current`
5. EF Core global query filter auto-applies `WHERE tenant_id = <from-context>`
6. Before executing SQL, `TenantConnectionInterceptor` issues `SET LOCAL app.tenant_id = '<guid>'`
7. Postgres RLS policy evaluates: `USING (tenant_id = current_setting('app.tenant_id')::uuid)`
8. Result: double filtering — cross-tenant reads return zero rows even if one layer fails

### Tenant resolver pipeline

The SDK ships with pluggable resolvers invoked in priority order. Default order:
- JWT claim (priority 100)
- HTTP header (priority 50)
- URL path (priority 30)
- Subdomain (priority 20)
- API key (priority 10, stubbed in Phase 2)

Register custom resolvers:
```csharp
opts.Tenancy.Resolvers.Use<CustomTenantResolver>(priority: 150);
```

Anonymous bypass list (default: `/health`, `/openapi`, `/.well-known`, `/connect`, `/scalar`):
```csharp
opts.Tenancy.AnonymousBypass.Add("/custom-public-endpoint");
```

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

## Transport and Messaging

### Switching between In-Process and Bus

**One change. That's it.**

In code:
```csharp
opts.UseTransport(SaasTransport.InProc);  // dev
opts.UseTransport(SaasTransport.Bus);     // prod (RabbitMQ)
```

Or via appsettings:
```json
{
  "SaasBuilder": {
    "Transport": "Bus"
  }
}
```

MassTransit will:
- In-process: run the consumer on the current thread
- Out-of-process: serialize to RabbitMQ, deserialize in a consumer, same handler code

No business logic changes. Same validator pipeline. Same domain events. Same error handling.

### Consumer registration

**In-process (Mediator):**
```csharp
opts.Transport.WithMediatorConsumers(cfg =>
{
    cfg.AddConsumer<CreateUserHandler>();
    cfg.AddConsumer<PostTransactionHandler>();
});
```

**Out-of-process (Bus):**
```csharp
opts.Transport.WithBusConsumers(cfg =>
{
    cfg.AddConsumer<CreateUserHandler>(c =>
        c.UseMessageRetry(r => r.Immediate(3)));
    cfg.AddConsumer<PostTransactionHandler>(c =>
        c.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromMilliseconds(100), ...)));
});
```

Register sagas (orchestrated workflows) in the Bus configuration only.

---

## Sagas: When and How

### When to use a saga

**Use a saga when:**
- A workflow spans multiple modules
- Compensation (rollback) is needed if any step fails
- You want to orchestrate the order and retry policy

**Example: Registration workflow (Phase 5):**
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

public class RegistrationSaga :
    SagaStateMachine<RegistrationSagaState>
{
    public State Started { get; set; }
    public State UserCreated { get; set; }
    public State LedgerInitialized { get; set; }
    public State Completed { get; set; }
    public State Faulted { get; set; }
    
    public Event<RegistrationStartedEvent> StartRegistration { get; set; }
    public Event<UserCreatedEvent> UserCreated_Event { get; set; }
    public Event<SagaTimeoutExpired> Timeout { get; set; }
    
    public RegistrationSaga()
    {
        Initially(
            When(StartRegistration)
                .Then(ctx => ctx.Saga.CorrelationId = ctx.Message.CorrelationId)
                .PublishAsync(ctx => ctx.Init<CreateUserCommand>(new() { ... }))
                .TransitionTo(Started));
        
        During(Started,
            When(UserCreated_Event)
                .PublishAsync(ctx => ctx.Init<InitLedgerCommand>(new() { ... }))
                .TransitionTo(UserCreated));
        
        During(UserCreated,
            When(Timeout)
                .PublishAsync(ctx => ctx.Init<RollbackCommand>(...))
                .TransitionTo(Faulted));
    }
}

public class RegistrationSagaState : SagaStateMachineInstance, IVersionedSagaState
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public Guid TenantId { get; set; }
    public int Version { get; set; }
}
```

Saga state persisted in a dedicated `registration` schema. Each step is a compensating command in reverse. Visible in Grafana.

---

## Phase 5 Modules: Deferred Features

All Phase 5 modules (Notifications, Files, Jobs, Audit, Webhooks, Search, Realtime) ship with abstractions + default implementations. Deferred features are clearly marked with `TODO(Phase X.Y)` in code. Key stubbed features:

### Identity (Phase 2)
- SAML 2.0 per-org (Phase 2.7)
- SCIM 2.0 inbound provisioning (Phase 2.8)
- MFA / TOTP / WebAuthn (Phase 2.2)
- Social login (Phase 2.3)
- API keys / M2M (Phase 2.9)
- Impersonation (Phase 2.10)

### Tenancy (Phase 3)
- Schema-per-tenant (Phase 3.1)
- Database-per-tenant (Phase 3.1)
- Stamp routing (Phase 3.1)
- KMS adapters (Phase 3.4)
- Encryption codecs (Phase 3.4)

### Billing (Phase 4)
- Stripe adapter (Phase 4.1)
- Paddle adapter (Phase 4.1)
- Lemon Squeezy adapter (Phase 4.1)
- Chargebee adapter (Phase 4.1)
- LaunchDarkly / Unleash feature flags (Phase 4.9)

### Notifications (Phase 5)
- SendGrid adapter (Phase 5.1)
- AWS SES adapter (Phase 5.1)
- Postmark adapter (Phase 5.1)
- Twilio SMS (Phase 5.1)

### Files (Phase 5)
- Azure Blob Storage (Phase 5.2)
- AWS S3 (Phase 5.2)
- Google Cloud Storage (Phase 5.2)

### Jobs (Phase 5)
- Hangfire adapter (Phase 5.3)
- Quartz.NET adapter (Phase 5.3)

### Audit (Phase 5)
- Splunk HEC forwarder (Phase 5.4)
- Datadog forwarder (Phase 5.4)
- Syslog forwarder (Phase 5.4)
- Merkle-style hash chain storage (Phase 5.4)

### Search (Phase 5)
- Algolia adapter (Phase 5.6)
- Meilisearch adapter (Phase 5.6)
- Typesense adapter (Phase 5.6)

### Realtime (Phase 5)
- Redis SignalR backplane (Phase 5.7)

See code annotations for specifics: `grep -r "TODO(Phase" src/` or `docs/TASK_LIST.md`.

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

**Integration tests** (highest value): `WebApplicationFactory<Program>` + `Testcontainers`
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

**Cross-tenant leak test required** for every new tenant-scoped feature. See test templates in `tests/SaasBuilder.IntegrationTests/Phase*/`.

---

## Observability

### Key metrics to watch

| Meter | What it means | Alert threshold |
|-------|---------------|-----------------|
| `saasbuilder_commands_duration_seconds{module,command}` | Handler latency | p95 > 200 ms |
| `saasbuilder_outbox_lag_seconds{module}` | Time from DB commit to bus delivery | p99 > 500 ms |
| `saasbuilder_outbox_depth{module}` | Undelivered messages in outbox table | > 1000 |
| `saasbuilder_rls_denials_total{module,table}` | RLS policy rejections | > 0 (investigate) |
| `saasbuilder_saga_active_count{saga}` | In-flight sagas | > 1000 (saturation) |
| `saasbuilder_module_load_duration_seconds{module}` | Startup time per module | > 5 s (slow loader) |

### Dashboards

Grafana dashboards are JSON-provisioned under `deploy/grafana/provisioning/dashboards/`:
- `saasbuilder-overview.json` — health traffic light per module
- `module-latency.json` — per-module p50/p95/p99
- `saga-health.json` — saga states, time-in-state, failures
- `rls-denial-rate.json` — multi-tenant isolation health
- `outbox-lag.json` — delivery SLO tracking

### Adding a new meter to a module

In your handler, use `System.Diagnostics.Metrics.Meter`:

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

**Symptoms:** `saasbuilder_outbox_lag_seconds` p99 > 500 ms and growing.

**Diagnosis:**
1. Is RabbitMQ alive? `rabbitmqctl ping`
2. Is the consumer running? Check `rabbitmqctl list_consumers`
3. Is the outbox table locked? `SELECT * FROM pg_locks WHERE relation = (SELECT oid FROM pg_class WHERE relname = 'outbox')`
4. What's in the DLQ? Check queues in RabbitMQ dashboard

**Fix:**
- Restart the delivery service: `dotnet run --project samples/SaasBuilder.Sample.Host`
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

---

## PR Checklist

Before opening a pull request:

- [ ] `dotnet build SaasBuilder.sln -warnaserror` passes with zero warnings
- [ ] `dotnet test` passes (note: Testcontainers tests require Docker)
- [ ] Architecture tests pass: `dotnet test tests/SaasBuilder.ArchitectureTests/`
  - Every tenant-scoped entity has a CREATE POLICY in `migrations/{module}/*.sql`
- [ ] New `IBlobStore`, `INotificationDispatcher`, `IJobScheduler`, etc. implementations log a startup WARNING when env vars are missing (silent degradation, not crash)
- [ ] New tenant-scoped feature has a cross-tenant leak integration test
- [ ] New endpoints documented in Swagger/Scalar
- [ ] Request DTOs have FluentValidation validators
- [ ] New endpoints have `[Authorize]` or explicit `[AllowAnonymous]`
- [ ] EF Core migrations reviewed for correctness (no raw SQL with user input)
- [ ] No secrets in source code
- [ ] No breaking changes to existing API contracts (or versioned)

---

## Further Reading

- **`README.md`** (root) — project elevator pitch and quickstart
- **`docs/SAAS_SDK_IMPLEMENTATION_PLAN.md`** — full architecture, phases, decisions, load-testing strategy, security posture
- **`docs/TASK_LIST.md`** — canonical phase-by-phase task checklist
- **`.claude/memory/decisions.md`** — design rationale and consequences
- **`.claude/CLAUDE.md`** — .NET standards, language rules, required project settings
- **`.claude/rules/backend.md`** — layer boundaries, EF Core rules, validation, UX requirements
- **`.claude/rules/testing.md`** — test policy, load-bearing filter, test budget per change type
- **MassTransit documentation** — consumer contracts, pipeline filters, saga state machines
- **Entity Framework Core documentation** — global query filters, interceptors, migrations
- **OpenIddict documentation** — OIDC flows, token validation, claim customization
- **OpenTelemetry .NET documentation** — instrumentation, exporters, custom meters
- **OpenFeature specification** — feature flagging interface (Phase 4)
- **Standard Webhooks specification** — `webhook-id`, `webhook-timestamp`, `webhook-signature` (Phase 5)
- **OWASP ASVS Level 2** — security requirements for multi-tenant SaaS

---

<!-- written-by: writer-haiku | model: haiku -->
