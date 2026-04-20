# C# Language Standards

## Project Settings (required)
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisLevel>latest</AnalysisLevel>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

## Preferred Patterns

### Record Types for Immutable Data
```csharp
// Commands and Queries — immutable, value equality
public record CreateJobCommand(
    string Title,
    string Description,
    Guid OrganizationId);

// Response DTOs
public record JobDto(
    Guid Id,
    string Title,
    string Status,
    DateTimeOffset CreatedAt);

// Value objects
public record Email
{
    public string Value { get; }
    private Email(string value) => Value = value;
    public static Result<Email> Create(string value)
        => IsValid(value) ? Result<Email>.Success(new Email(value)) : Result<Email>.Failure("Invalid email");
    private static bool IsValid(string v) => v.Contains('@') && v.Length <= 320;
}
```

### Primary Constructors (C# 12)
```csharp
// Services with dependencies
public class JobService(
    IJobRepository jobs,
    IOrganizationRepository orgs,
    ILogger<JobService> logger)
{
    public async Task<Result<JobDto>> CreateAsync(
        CreateJobCommand cmd,
        CancellationToken ct = default)
    {
        // ...
    }
}
```

### Result Pattern (for expected failures)
```csharp
public class Result<T>
{
    public T? Value { get; private init; }
    public string? Error { get; private init; }
    public bool IsSuccess => Error is null;

    private Result() {}

    public static Result<T> Success(T value) => new() { Value = value };
    public static Result<T> Failure(string error) => new() { Error = error };
}

// Usage
var result = await handler.Handle(command, ct);
if (!result.IsSuccess)
    return BadRequest(new ProblemDetails { Detail = result.Error });
return Ok(result.Value);
```

### Pattern Matching
```csharp
// Switch expressions over if-else chains
var message = user.Status switch
{
    UserStatus.Active => "Welcome back",
    UserStatus.Suspended => "Account suspended",
    UserStatus.Pending => "Please verify your email",
    _ => throw new InvalidOperationException($"Unknown status: {user.Status}")
};

// Property patterns
if (user is { IsActive: true, Role: UserRole.Admin } admin)
{
    await admin.ProcessAdminActionAsync(ct);
}

// List patterns (C# 11)
if (errors is [var single])
    return BadRequest(single);
```

### Async/Await Standards
```csharp
// Always thread CancellationToken
public async Task<IReadOnlyList<JobDto>> GetJobsAsync(
    Guid orgId,
    CancellationToken ct = default)
{
    return await _context.Jobs
        .AsNoTracking()
        .Where(j => j.OrganizationId == orgId)
        .Select(j => new JobDto(j.Id, j.Title, j.Status, j.CreatedAt))
        .ToListAsync(ct);     // ← always pass ct
}

// ConfigureAwait(false) in library/infrastructure code
var result = await _httpClient
    .GetFromJsonAsync<ExternalDto>(url, ct)
    .ConfigureAwait(false);

// ValueTask for hot paths with frequent synchronous completion
public ValueTask<bool> IsValidAsync(string token)
{
    if (_cache.TryGetValue(token, out var cached))
        return ValueTask.FromResult(cached);     // synchronous fast path
    return new ValueTask<bool>(ValidateFromDbAsync(token));
}
```

### Exception Handling
```csharp
// Custom domain exceptions — throw only for unexpected/programming errors
public class DomainException(string message) : Exception(message);
public class NotFoundException(string entityType, object id)
    : DomainException($"{entityType} '{id}' was not found.");

// DO: Use Result for expected failures
public async Task<Result<Job>> GetJobAsync(Guid id, CancellationToken ct)
{
    var job = await _context.Jobs.FindAsync([id], ct);
    return job is null
        ? Result<Job>.Failure($"Job {id} not found")
        : Result<Job>.Success(job);
}

// DON'T: throw for expected failures
// ❌ throw new Exception("not found");  // forces try/catch at every call site
```

### Nullable Reference Types
```csharp
// Model intent clearly
public string? MiddleName { get; set; }          // nullable — optional field
public required string LastName { get; init; }    // non-nullable, must be set
public string FirstName { get; private set; } = string.Empty;  // non-nullable with default

// Always check FirstOrDefault results
var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
if (user is null)
    return Result<User>.Failure("User not found");

// When null is impossible, document why
var admin = _context.Users.First(u => u.Role == UserRole.Admin);
// Safe: seeded in migrations; at least one admin always exists per DB constraint
```

## Forbidden

- `async void` — except UI event handlers; always use `async Task`
- `.Result` or `.Wait()` — deadlock in ASP.NET; always `await`
- `Thread.Sleep` — use `await Task.Delay(ms, ct)` instead
- `dynamic` at domain/application boundaries — use proper types or `object` with type guards
- Empty catch blocks — `catch (Exception) {}` swallows errors silently; at minimum log
- `!` null-forgiving without a comment explaining why null is impossible
- `new()` expressions for `List<T>` return types in hot paths — prefer `IReadOnlyList<T>` for API surface

## Naming Conventions

| Construct | Convention | Example |
|---|---|---|
| Classes, records, structs | PascalCase | `JobService`, `CreateJobCommand` |
| Interfaces | `I` + PascalCase | `IJobRepository` |
| Methods | PascalCase | `GetJobsByOrgAsync` |
| Async methods | `*Async` suffix | `CreateJobAsync` |
| Private fields | `_camelCase` | `_jobRepository` |
| Constants | `PascalCase` | `MaxTitleLength = 200` |
| Enums | PascalCase + singular | `JobStatus.Active` |
| Generic type params | `T`, or `TEntity`, `TResult` | `Result<TValue>` |
