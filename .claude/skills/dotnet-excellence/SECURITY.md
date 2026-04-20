# Security Standards (.NET)

## Authentication & Authorization

### JWT Configuration (Secure Baseline)
```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,            // MUST: reject tokens from unknown issuers
            ValidateAudience = true,          // MUST: reject tokens intended for other services
            ValidateLifetime = true,          // MUST: reject expired tokens
            ValidateIssuerSigningKey = true,  // MUST: verify signature
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(1)  // tight tolerance
        };
    });
```

### Authorization Patterns
```csharp
// Policy-based (preferred over roles for complex rules)
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("RequireAdmin", p => p.RequireRole("Admin", "SuperAdmin"));
    o.AddPolicy("SameOrganization", p => p.AddRequirements(new SameOrgRequirement()));
    o.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Endpoint-level policies
[Authorize(Policy = "RequireAdmin")]
[HttpDelete("{id:guid}")]
public async Task<IActionResult> DeleteJob(Guid id, CancellationToken ct) { ... }

// AllowAnonymous is explicit opt-out — not a default
[AllowAnonymous]
[HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct) { ... }
```

### Resource-Based Authorization
```csharp
// Check ownership in handler, not controller
public class DeleteJobHandler(
    IJobRepository jobs,
    IAuthorizationService authz,
    IHttpContextAccessor http) : IRequestHandler<DeleteJobCommand, Result>
{
    public async Task<Result> Handle(DeleteJobCommand cmd, CancellationToken ct)
    {
        var job = await jobs.GetByIdAsync(cmd.JobId, ct);
        if (job is null) return Result.Failure("Not found");

        var authResult = await authz.AuthorizeAsync(
            http.HttpContext!.User, job, "JobOwnerPolicy");

        if (!authResult.Succeeded)
            return Result.Failure("Forbidden");

        await jobs.DeleteAsync(job, ct);
        return Result.Success();
    }
}
```

## Secret Management

### Never in Source Code
```csharp
// ❌ Never
var key = "my-super-secret-jwt-key-hardcoded";

// ✅ Always use configuration abstraction
var key = configuration["Jwt:Key"];  // resolved from environment or secret store
```

### Secret Sources (in order of preference)
1. Azure Key Vault / AWS Secrets Manager / HashiCorp Vault (production)
2. Docker secrets / Kubernetes secrets (container deployments)
3. Environment variables (`JWT__KEY=...` maps to `Jwt:Key` in .NET)
4. `dotnet user-secrets` (local development only — never committed)

### Never Store These in appsettings.json
- JWT signing keys
- Database passwords in connection strings
- External API keys
- OAuth client secrets

## Input Validation

### FluentValidation (API boundary)
```csharp
public class CreateJobValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobValidator()
    {
        RuleFor(r => r.Title)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(@"^[a-zA-Z0-9\s\-_.,!?()]+$")  // allowlist, not denylist
            .WithMessage("Title contains invalid characters");

        // Never trust IDs from unauthenticated sources
        RuleFor(r => r.OrganizationId)
            .NotEmpty();
    }
}
```

### Domain Invariants (defense in depth)
```csharp
public class Job
{
    public static Job Create(string title, string description, Guid orgId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Job title cannot be empty");
        if (title.Length > 200)
            throw new DomainException("Job title cannot exceed 200 characters");
        // ...
        return new Job { Title = title, Description = description, OrganizationId = orgId };
    }
}
```

## SQL Injection Prevention

EF Core parameterizes all LINQ queries automatically. Risk only arises with raw SQL:

```csharp
// ❌ SQL injection risk
var jobs = db.Jobs.FromSqlRaw($"SELECT * FROM Jobs WHERE Title = '{userInput}'");

// ✅ Parameterized (safe)
var jobs = db.Jobs.FromSqlRaw("SELECT * FROM Jobs WHERE Title = {0}", userInput);

// ✅ Better: use LINQ — parameterized automatically
var jobs = db.Jobs.Where(j => j.Title == userInput);

// ✅ For bulk operations with user input — use ExecuteSqlInterpolated
await db.Database.ExecuteSqlInterpolatedAsync(
    $"UPDATE Jobs SET Status = {status} WHERE OrganizationId = {orgId}", ct);
```

## CORS Configuration

```csharp
builder.Services.AddCors(o =>
{
    o.AddPolicy("Production", policy =>
    {
        policy
            .WithOrigins("https://app.yourcompany.com")  // explicit, never AllowAnyOrigin
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();                          // only with specific origins
    });

    o.AddPolicy("Development", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Apply correct policy based on environment
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");
```

## Password Handling

```csharp
// ✅ BCrypt (via BCrypt.Net-Next NuGet)
var hash = BCrypt.HashPassword(plainPassword, workFactor: 12);
var valid = BCrypt.Verify(plainPassword, hash);

// ✅ ASP.NET Core Identity (handles hashing automatically)
await userManager.CreateAsync(user, plainPassword);  // hashed internally

// ❌ Never
var hash = MD5.HashData(Encoding.UTF8.GetBytes(password));   // broken
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password)); // broken for passwords
```

## Sensitive Data in Logs

```csharp
// ❌ Never log sensitive fields
logger.LogInformation("User login: {Email} with password {Password}", email, password);

// ✅ Log identifiers, not content
logger.LogInformation("User login succeeded: {UserId}", user.Id);

// Use [LoggerMessage] source generator for structured, high-performance logging
[LoggerMessage(Level = LogLevel.Warning, Message = "Failed login attempt for user {UserId}")]
partial void LogFailedLogin(Guid userId);
```

## Security Response Headers

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
```

## Security Checklist (Pre-PR)

- [ ] No secrets in source code or appsettings.json
- [ ] All new endpoints have `[Authorize]` or explicit `[AllowAnonymous]`
- [ ] JWT validates issuer, audience, lifetime, and signing key
- [ ] No `FromSqlRaw` with string interpolation
- [ ] CORS explicitly configured with allowed origins (no `AllowAnyOrigin` + `AllowCredentials`)
- [ ] Passwords hashed with BCrypt/Argon2 (never MD5/SHA1/SHA256 raw)
- [ ] Sensitive data not in URL query strings or logs
- [ ] Rate limiting on auth endpoints
- [ ] Input validation on all request DTOs
