# Testing Standards (.NET)

## Stack
- **xUnit** — test framework
- **Moq** — mocking
- **FluentAssertions** — readable assertions
- **TestContainers** — real databases in integration tests
- **Microsoft.AspNetCore.Mvc.Testing** — `WebApplicationFactory` for API integration tests
- **Bogus** — realistic fake data generation (optional)

## Test Projects

```
tests/
├── YourProject.UnitTests/          # Handler, domain, validation logic
│   ├── Jobs/
│   │   ├── CreateJobHandlerTests.cs
│   │   └── JobTests.cs             # Domain entity tests
│   └── Common/
│       └── ResultTests.cs
└── YourProject.IntegrationTests/   # API + EF Core + real database
    ├── Setup/
    │   ├── WebAppFactory.cs        # WebApplicationFactory<Program>
    │   └── DatabaseFixture.cs      # TestContainers PostgreSQL
    └── Jobs/
        ├── CreateJobTests.cs
        └── GetJobsTests.cs
```

## Integration Tests (Preferred — highest value)

Use `WebApplicationFactory` + TestContainers for real database behavior:

```csharp
// WebAppFactory.cs
public class WebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithDatabase("testdb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace real DB with TestContainers instance
            var descriptor = services.Single(s => s.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<AppDbContext>(o =>
                o.UseNpgsql(_db.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        // Apply migrations
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _db.DisposeAsync();

    public HttpClient CreateAuthenticatedClient(string role = "User")
    {
        var client = CreateClient();
        var token = GenerateTestToken(role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

// Integration test
public class CreateJobTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task CreateJob_WhenValidRequest_ReturnsCreated()
    {
        var client = factory.CreateAuthenticatedClient();
        var request = new { Title = "Senior Developer", Description = "Build great things", OrganizationId = SeedData.OrgId };

        var response = await client.PostAsJsonAsync("/api/v1/jobs", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JobDto>();
        body!.Title.Should().Be("Senior Developer");
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateJob_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();  // no auth header
        var response = await client.PostAsJsonAsync("/api/v1/jobs", new { Title = "test" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateJob_WhenTitleMissing_ReturnsBadRequest()
    {
        var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/jobs",
            new { Title = "", Description = "desc", OrganizationId = SeedData.OrgId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Title");
    }
}
```

## Handler Unit Tests

Mock the repository, test the handler's logic:

```csharp
public class CreateJobHandlerTests
{
    private readonly Mock<IJobRepository> _jobs = new();
    private readonly Mock<IOrganizationRepository> _orgs = new();
    private readonly CreateJobHandler _sut;

    public CreateJobHandlerTests()
    {
        _sut = new CreateJobHandler(_jobs.Object, _orgs.Object);
    }

    [Fact]
    public async Task Handle_WhenOrgNotFound_ReturnsFailure()
    {
        _orgs.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(false);

        var result = await _sut.Handle(
            new CreateJobCommand("title", "desc", Guid.NewGuid()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("organization");
        _jobs.Verify(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesJobAndReturnsId()
    {
        var orgId = Guid.NewGuid();
        _orgs.Setup(r => r.ExistsAsync(orgId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);
        _jobs.Setup(r => r.AddAsync(It.IsAny<Job>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        var result = await _sut.Handle(
            new CreateJobCommand("Senior Developer", "Build things", orgId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }
}
```

## Domain Entity Tests

Pure logic — no mocks, no EF Core, no DI:

```csharp
public class JobTests
{
    [Fact]
    public void Create_WhenTitleIsEmpty_ThrowsDomainException()
    {
        var act = () => Job.Create("", "description", Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("*title*");
    }

    [Fact]
    public void Create_WhenValid_SetsPropertiesCorrectly()
    {
        var orgId = Guid.NewGuid();
        var job = Job.Create("Senior Developer", "Build things", orgId);

        job.Title.Should().Be("Senior Developer");
        job.OrganizationId.Should().Be(orgId);
        job.Status.Should().Be(JobStatus.Draft);
        job.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
```

## Validation Tests

Test the validator itself — one test per distinct rule, not per input value:

```csharp
public class CreateJobValidatorTests
{
    private readonly CreateJobRequestValidator _sut = new();

    [Fact]
    public void Validate_WhenTitleIsEmpty_HasTitleError()
    {
        var result = _sut.TestValidate(new CreateJobRequest("", "desc", Guid.NewGuid()));
        result.ShouldHaveValidationErrorFor(r => r.Title);
    }

    [Fact]
    public void Validate_WhenAllValid_HasNoErrors()
    {
        var result = _sut.TestValidate(
            new CreateJobRequest("Senior Dev", "Build things", Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

## Test Organization

```csharp
// Naming: MethodOrScenario_WhenCondition_ExpectedResult
public class CreateJobHandlerTests
{
    [Fact]
    public async Task Handle_WhenOrgNotFound_ReturnsFailure() { }

    [Fact]
    public async Task Handle_WhenDuplicateTitle_ReturnsConflict() { }

    [Fact]
    public async Task Handle_WhenValid_CreatesAndReturnsId() { }
}

// Trait categories for filtering
[Trait("Category", "Unit")]
public class CreateJobHandlerTests { }

[Trait("Category", "Integration")]
public class CreateJobApiTests { }
```

## Test Data Factories

```csharp
// Simple, deterministic factories — no Bogus for critical tests
public static class TestData
{
    public static CreateJobCommand ValidCreateJobCommand(
        string title = "Senior Developer",
        Guid? orgId = null) =>
        new(title, "Build great things", orgId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"));

    public static Job ValidJob(Guid? id = null) =>
        Job.Create("Senior Developer", "Build things", Guid.NewGuid()) with
        { Id = id ?? Guid.NewGuid() };
}
```

## What NOT to Test (Ban List extension for .NET)

- `record` equality: `new JobDto("x", 1) == new JobDto("x", 1)` — testing the language
- FluentValidation built-in rules: `NotEmpty()` on empty string — testing the library
- EF Core `SaveChanges` returns count — testing EF Core
- ASP.NET Core model binding: `[FromBody]` correctly deserializes JSON — testing the framework
- `Mock.Verify` with no outcome assertion — proves your mock was called, not that your code did something useful
- `Should().NotBeNull()` as the only assertion — not meaningful
- Duplicate happy-path tests with slightly different data
