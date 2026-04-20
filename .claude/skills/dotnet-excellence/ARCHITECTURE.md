# Architecture Standards

## Clean Architecture

### When to Use
- Complex domain with rich business rules
- Large team (>3 devs touching the same codebase)
- Long-lived codebase (>1 year)
- DDD (Domain-Driven Design) approach

### Layer Dependency Rule
```
API → Application → Domain
          ↓
    Infrastructure (implements Application interfaces)
```
- Domain has ZERO external dependencies
- Application depends only on Domain + abstractions
- Infrastructure implements Application interfaces (repositories, external services)
- API depends on Application (not Infrastructure directly, except DI registration)

### Project Structure
```
YourProject.Domain/
├── Entities/
│   ├── Job.cs
│   └── Organization.cs
├── ValueObjects/
│   ├── Email.cs
│   └── JobStatus.cs
├── Events/
│   └── JobCreatedEvent.cs
├── Exceptions/
│   ├── DomainException.cs
│   └── NotFoundException.cs
└── Interfaces/
    └── IJobRepository.cs          # Abstract interface only — no EF Core here

YourProject.Application/
├── Jobs/
│   ├── Commands/
│   │   ├── CreateJob/
│   │   │   ├── CreateJobCommand.cs
│   │   │   ├── CreateJobHandler.cs
│   │   │   └── CreateJobValidator.cs
│   │   └── DeleteJob/
│   └── Queries/
│       ├── GetJobById/
│       │   ├── GetJobByIdQuery.cs
│       │   └── GetJobByIdHandler.cs
│       └── GetJobs/
├── Common/
│   ├── Interfaces/
│   │   └── IAppDbContext.cs       # EF Core abstraction for testability
│   ├── Models/
│   │   └── Result.cs
│   └── Behaviours/               # MediatR pipeline behaviors (optional)
│       ├── ValidationBehaviour.cs
│       └── LoggingBehaviour.cs
└── DTOs/
    ├── JobDto.cs
    └── PagedResult.cs

YourProject.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs            # Implements IAppDbContext
│   ├── Configurations/
│   │   └── JobConfiguration.cs
│   └── Repositories/
│       └── JobRepository.cs
├── Identity/
│   └── TokenService.cs
└── DependencyInjection.cs         # Extension method to register infrastructure services

YourProject.API/
├── Controllers/
│   └── JobsController.cs
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
├── Filters/
│   └── ValidationFilter.cs
└── Program.cs
```

## Vertical Slice Architecture

### When to Use
- CRUD-heavy application
- Microservice with a narrow domain
- Smaller team preferring co-location over abstraction
- Rapid development pace

### Structure
```
YourProject.API/
├── Features/
│   ├── Jobs/
│   │   ├── CreateJob/
│   │   │   ├── CreateJobEndpoint.cs    # OR CreateJobController.cs
│   │   │   ├── CreateJobCommand.cs
│   │   │   ├── CreateJobHandler.cs
│   │   │   └── CreateJobValidator.cs
│   │   ├── GetJobById/
│   │   │   ├── GetJobByIdEndpoint.cs
│   │   │   ├── GetJobByIdQuery.cs
│   │   │   └── GetJobByIdHandler.cs
│   │   └── GetJobs/
│   └── Candidates/
├── Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   └── Configurations/
│   ├── Identity/
│   └── DependencyInjection.cs
├── Common/
│   ├── Models/
│   │   ├── Result.cs
│   │   └── PagedResult.cs
│   └── Extensions/
└── Program.cs
```

### Slice Rules
- Each slice owns its request/response DTOs — no sharing between slices (duplication is OK)
- Slices share only infrastructure (DbContext, external service clients) and common models (Result, PagedResult)
- Slices may NOT import from each other's feature folders

## Layer Boundary Enforcement

Use ArchUnitNET or NetArchTest to enforce at test time:

```csharp
// YourProject.ArchitectureTests
[Fact]
public void DomainLayer_ShouldNotDependOn_ApplicationOrInfrastructure()
{
    var domain = Types.InAssembly(typeof(Job).Assembly);
    var result = domain.ShouldNot()
        .HaveDependencyOnAny(
            typeof(AppDbContext).Namespace,
            typeof(CreateJobHandler).Namespace)
        .GetResult();

    result.IsSuccessful.Should().BeTrue(result.FailingRules.First().ToString());
}

[Fact]
public void ApplicationLayer_ShouldNotDependOn_Infrastructure()
{
    var application = Types.InAssembly(typeof(CreateJobHandler).Assembly);
    var result = application.ShouldNot()
        .HaveDependencyOn(typeof(AppDbContext).Namespace)
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

## CQRS Decision Matrix

| Scenario | Recommendation |
|---|---|
| Simple CRUD | Direct service calls (no CQRS) |
| Separate read/write models needed | CQRS without MediatR |
| Cross-cutting behaviors (logging, validation, caching) on all operations | MediatR with pipeline behaviors |
| Event sourcing | CQRS + event store mandatory |
| Microservice with single feature | Direct service calls |

### MediatR Usage (when justified)
```csharp
// Command
public record CreateJobCommand(string Title, string Description, Guid OrgId)
    : IRequest<Result<Guid>>;

// Handler
public class CreateJobHandler(IAppDbContext db) : IRequestHandler<CreateJobCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateJobCommand cmd, CancellationToken ct)
    {
        // ... validate, create, save
        return Result<Guid>.Success(job.Id);
    }
}

// Controller
public async Task<IActionResult> CreateJob(
    CreateJobRequest req,
    ISender sender,
    CancellationToken ct)
{
    var result = await sender.Send(new CreateJobCommand(req.Title, req.Description, req.OrgId), ct);
    return result.IsSuccess
        ? CreatedAtAction(nameof(GetJob), new { id = result.Value }, null)
        : BadRequest(new ProblemDetails { Detail = result.Error });
}
```

## Common Pitfalls

- **Anemic domain model** — entities that are just bags of properties with no behavior. Put business rules in the entity.
- **Fat controllers** — business logic creeping into controllers over time. Keep controllers to 5-10 lines per action.
- **Repository returning `IQueryable<T>`** — breaks encapsulation and allows callers to build arbitrary queries, including N+1s.
- **Application layer depending on `DbContext` directly** — breaks testability and layer boundaries. Use `IAppDbContext` interface.
- **Over-engineering a simple CRUD app** — don't apply Clean Architecture to a 10-endpoint CRUD service. Vertical Slice or single-project is fine.
