# ASP.NET Core Standards

## Startup Configuration

### Program.cs (Minimal Hosting Model)
```csharp
var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddScoped<IJobService, JobService>();

// Auth
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Middleware — ORDER MATTERS
app.UseExceptionHandler("/error");    // first — catches everything downstream
app.UseHsts();
app.UseHttpsRedirection();
app.UseAuthentication();              // before Authorization
app.UseAuthorization();
app.MapControllers();

app.Run();
```

## Controllers

### Controller Pattern
```csharp
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]   // default: require auth on all actions
public class JobsController(IJobService jobService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<JobSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await jobService.GetJobsAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType<JobDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJob(
        [FromBody] CreateJobRequest request,
        CancellationToken ct = default)
    {
        var result = await jobService.CreateJobAsync(request, ct);
        if (!result.IsSuccess)
            return BadRequest(new ProblemDetails { Detail = result.Error });
        return CreatedAtAction(nameof(GetJob), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<JobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken ct = default)
    {
        var result = await jobService.GetJobAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }
}
```

### No Business Logic in Controllers
Controllers are HTTP plumbing only:
1. Parse and validate request (FluentValidation does validation automatically via filter)
2. Call application service / handler
3. Map result to HTTP response

Never:
- Put domain logic or business rules in controllers
- Access `DbContext` directly from controllers
- Return EF Core entities from controllers

## Minimal APIs (for simple endpoints / microservices)

```csharp
// Group related endpoints
var jobs = app.MapGroup("/api/v1/jobs")
    .RequireAuthorization();   // default auth for the group

jobs.MapGet("/", async (IJobService svc, CancellationToken ct) =>
    Results.Ok(await svc.GetJobsAsync(ct)));

jobs.MapPost("/", async (CreateJobRequest req, IJobService svc, CancellationToken ct) =>
{
    var result = await svc.CreateJobAsync(req, ct);
    return result.IsSuccess
        ? Results.Created($"/api/v1/jobs/{result.Value!.Id}", result.Value)
        : Results.BadRequest(new ProblemDetails { Detail = result.Error });
});

jobs.MapGet("/{id:guid}", async (Guid id, IJobService svc, CancellationToken ct) =>
{
    var result = await svc.GetJobAsync(id, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
});
```

## FluentValidation Integration

```csharp
// Validator definition
public class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(r => r.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(r => r.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(r => r.OrganizationId)
            .NotEmpty().WithMessage("OrganizationId is required");
    }
}

// Auto-registration — no per-validator wiring needed
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Auto-validation filter (validates before controller action runs)
builder.Services.AddFluentValidationAutoValidation();
// Returns 400 ProblemDetails automatically on validation failure
```

## Global Exception Middleware

```csharp
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "Resource not found");
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 404,
                Title = "Not Found",
                Detail = ex.Message
            });
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain rule violated");
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 422,
                Title = "Business Rule Violation",
                Detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 500,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred. Please try again later."
            });
        }
    }
}
```

## Dependency Injection Patterns

```csharp
// Lifetimes
builder.Services.AddScoped<IJobService, JobService>();      // per-request (most services)
builder.Services.AddSingleton<ITokenValidator, JwtValidator>(); // stateless singletons
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>(); // create fresh each time

// Extension methods for module registration (keep Program.cs lean)
public static class JobsModuleExtensions
{
    public static IServiceCollection AddJobsModule(this IServiceCollection services)
    {
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IJobService, JobService>();
        services.AddValidatorsFromAssemblyContaining<CreateJobRequestValidator>();
        return services;
    }
}

// Program.cs
builder.Services.AddJobsModule();
```

## Pagination Standard
```csharp
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrev => Page > 1;
}
```

## Rate Limiting (.NET 7+)
```csharp
builder.Services.AddRateLimiter(o =>
{
    o.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// On auth endpoints
app.MapPost("/auth/login", LoginHandler).RequireRateLimiting("auth");
```
