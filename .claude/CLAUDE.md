# .NET Backend Stack Configuration

## Token Discipline (mandatory)
- Do not ask me to paste long logs/dumps/configs into chat.
- If you need large output, instruct me to save it to a file and reference it with @path.
- If I paste >150 lines or >10k characters, stop and ask me to move it into a file, then continue once I provide @file.
- Prefer running commands with output redirected to a file (>, 2>, | tee) and then read that file.

## Always-load project context
Before planning or coding, read:
- `CHANGELOG_AI.md` (root) — recent implementation history
- The `dotnet-excellence` skill — `.claude/skills/dotnet-excellence/`

---

## Runtime & Tooling

| Component | Choice |
|-----------|--------|
| Runtime | .NET 6 LTS / .NET 8 LTS / .NET 10 |
| Language | C# 12+ |
| Framework | ASP.NET Core (controllers for complex APIs, Minimal APIs for simple/microservices) |
| ORM | Entity Framework Core 6/8 |
| Validation | FluentValidation |
| Mapping | Mapster (preferred) / AutoMapper |
| Testing | xUnit + Moq + FluentAssertions |
| Integration Testing | TestContainers + WebApplicationFactory |
| Logging | Serilog (structured logging) |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Caching | IMemoryCache / IDistributedCache (Redis) |
| API Docs | Scalar / Swagger (Swashbuckle) |
| Architecture | Clean Architecture or Vertical Slice Architecture |

---

## Project Structure

### Clean Architecture (default for complex domains)
```
src/
├── YourProject.Domain/           # Entities, value objects, domain events, interfaces
├── YourProject.Application/      # Use cases, DTOs, CQRS handlers, service interfaces
├── YourProject.Infrastructure/   # EF Core, repositories, external services, caching
└── YourProject.API/              # Controllers/endpoints, middleware, DI registration
tests/
├── YourProject.UnitTests/        # Application/domain logic tests
├── YourProject.IntegrationTests/ # API + EF Core + infrastructure tests
└── YourProject.ArchitectureTests/# Enforce layer dependency rules (NetArchTest)
```

### Vertical Slice Architecture (alternative for CRUD-heavy services)
```
src/
├── Features/
│   ├── Jobs/
│   │   ├── CreateJob/
│   │   │   ├── CreateJobCommand.cs
│   │   │   ├── CreateJobHandler.cs
│   │   │   ├── CreateJobValidator.cs
│   │   │   └── CreateJobEndpoint.cs
│   │   ├── GetJobs/
│   │   └── DeleteJob/
│   └── Candidates/
├── Infrastructure/               # Shared infrastructure: DbContext, auth, middleware
└── Common/                       # Shared value objects, result types, base classes
```

---

## C# Language Rules

### Required Project Settings
```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<AnalysisLevel>latest</AnalysisLevel>
```

### Preferred Patterns
```csharp
// Record types for immutable DTOs and commands
public record CreateJobRequest(string Title, string Description, Guid OrganizationId);

// Result pattern — avoid throwing for expected failures
public record Result<T>
{
    public T? Value { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new() { Value = value };
    public static Result<T> Failure(string error) => new() { Error = error };
}

// CancellationToken on every async method
public async Task<IReadOnlyList<JobDto>> GetJobsAsync(
    Guid orgId,
    CancellationToken ct = default)

// Pattern matching over nullable chains
if (user is { IsActive: true, Role: UserRole.Admin } activeAdmin)
{
    // ...
}

// Primary constructors (C# 12)
public class JobService(IJobRepository jobs, ILogger<JobService> logger)
{
    public async Task<Result<JobDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        // ...
    }
}
```

### Forbidden
- `async void` except for UI event handlers
- `.Result` or `.Wait()` on tasks (deadlock risk in ASP.NET)
- `Thread.Sleep` instead of `await Task.Delay`
- Swallowed exceptions — empty `catch {}` or `catch (Exception) {}`
- `any` equivalent: `object`, `dynamic` at domain boundaries without justification
- Exposing EF Core `DbSet` entities directly in API responses
- Mutable public setters on domain entities (`private set` or `init` required)
- Non-null assertions (`!`) without a documented comment explaining why it cannot be null

---

## Entity Framework Core Rules

### Query Rules
- Always use `AsNoTracking()` for read-only queries
- Always project to DTOs with `.Select()` — never return full entities from repositories
- Always pass `CancellationToken` to async EF operations (`ToListAsync(ct)`, `FirstOrDefaultAsync(ct)`)
- Never use `Include()` for lazy loading in Application layer — use projection
- Use `IQueryable<T>` composition; materialize only at the last step

```csharp
// Correct: projection at the query level
var jobs = await _context.Jobs
    .AsNoTracking()
    .Where(j => j.OrganizationId == orgId && !j.IsDeleted)
    .Select(j => new JobDto(j.Id, j.Title, j.Status))
    .ToListAsync(ct);

// Wrong: load entity then map
var entities = await _context.Jobs.ToListAsync(ct);
return entities.Select(j => mapper.Map<JobDto>(j));
```

### Migration Rules
- Every migration must be reviewed before applying to staging/production
- Migrations that drop columns or tables require a two-phase deployment
- Never use `EnsureCreated()` in production — use `Migrate()`
- Seed data goes in `IEntityTypeConfiguration<T>.Configure()` or `HasData()` (static only)

### DbContext Design
- One `DbContext` per bounded context (not one giant context)
- Expose `DbContext` only in `Infrastructure` layer — never inject into `Application` or `API`
- Use `IApplicationDbContext` interface for testability in Application layer

---

## ASP.NET Core Rules

### Endpoint Rules
- All endpoints authenticated by default (`[Authorize]` on controllers or global policy)
- Unauthenticated endpoints explicitly marked `[AllowAnonymous]`
- Request DTOs validated via FluentValidation registered in DI
- Controllers return `IActionResult` or typed `Results<T>` (Minimal APIs)
- No business logic in controllers — delegate to Application layer
- No `try/catch` in controllers — use global exception middleware

### Middleware Order (required)
```csharp
app.UseExceptionHandler();       // Must be first
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();         // Before Authorization
app.UseAuthorization();
app.MapControllers();
```

### API Design
- Use `[HttpGet("{id:guid}")]` — constrain route parameters with types
- Return `ProblemDetails` (RFC 7807) for all error responses
- Paginate all list endpoints — never return unbounded collections
- Version APIs via URL prefix (`/api/v1/`) or header

---

## Security Rules

- Validate JWT signature, audience, and issuer — never trust unsigned tokens
- Store secrets in environment variables or Azure Key Vault — never in `appsettings.json`
- Rate limit auth endpoints and sensitive operations
- Hash passwords with BCrypt or PBKDF2 — never store plain or MD5/SHA1
- CORS: explicit allowed origins, never `AllowAnyOrigin` + `AllowCredentials`
- SQL injection is prevented by EF Core parameterization — never concatenate raw SQL with user input
- `FromBody` for POST/PUT/PATCH — never use query string for sensitive data
- HTTPS enforced; HSTS enabled in production

---

## Testing Standards
Test authoring governed by `.claude/rules/testing.md` — load-bearing tests only.

### Required Coverage
- All command/query handlers (happy path + validation errors + not-found)
- All validation rules
- Authentication and authorization rules
- EF Core queries that contain non-trivial logic (projection, filtering)

### Testing Style
```csharp
public class CreateJobHandlerTests
{
    [Fact]
    public async Task Handle_WhenTitleIsEmpty_ReturnsValidationError()
    {
        // Arrange
        var handler = new CreateJobHandler(/*...*/);
        var command = new CreateJobCommand(Title: "", Description: "desc");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("title");
    }
}
```

---

## Common Commands

```bash
# Build
dotnet build

# Build with warnings as errors
dotnet build -warnaserror

# Run tests
dotnet test
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"

# Coverage
dotnet test --collect:"XPlat Code Coverage"

# EF Core migrations
dotnet ef migrations add <MigrationName> --project YourProject.Infrastructure
dotnet ef database update --project YourProject.Infrastructure
dotnet ef migrations list
dotnet ef migrations remove

# Format
dotnet format

# Watch mode
dotnet watch run --project YourProject.API
dotnet watch test
```

---

## Pre-PR Checklist

- `dotnet build -warnaserror` passes with no errors or warnings
- `dotnet test` passes for changed behavior
- All nullable warnings resolved
- New endpoints documented in Swagger/Scalar
- Request DTOs have FluentValidation validators
- New endpoints have `[Authorize]` or explicit `[AllowAnonymous]`
- EF Core migrations reviewed for correctness
- No raw SQL with user input (N+1 queries checked)
- No secrets in source code
- No breaking changes to existing API contracts (or versioned)
- Integration tests cover new endpoints
