# Testing Rules (.NET)

## Scope
Applies when editing `tests/**` or when any agent is authoring or reviewing tests.

This file is the **canonical source** for test-value policy in this project. Agent prompts reference it — do not duplicate the content into agent prompts, point at this file instead.

## Core Principle: Load-Bearing Tests Only

Every test must earn its place. A test earns its place only if it would **fail when the feature is broken in a way that causes visible pain**.

Guiding aphorisms:
- "Write tests. Not too many. Mostly integration." (Kent C. Dodds / Guillermo Rauch)
- Test behavior, not implementation. If refactoring the internals breaks the test, the test was testing the wrong thing.
- Prefer integration tests over over-mocked unit tests for .NET — `WebApplicationFactory` + TestContainers catch real bugs.

## The Load-Bearing Filter

Before writing any test, answer **yes to all four**:

1. **Failure signal** — If a realistic bug were introduced in the code under test, would *this specific test* fail?
2. **User-visible consequence** — If the asserted behavior were broken in production, would a user or downstream service notice?
3. **Non-redundant** — Is there no other test in the suite that would already catch the same bug?
4. **Not testing the framework** — Am I testing my code, or am I testing FluentValidation / EF Core / ASP.NET Core / xUnit?

Proposed tests that fail any question get dropped, not weakened.

## Ban List — Do Not Write These

- **FluentValidation echo tests** — testing that an empty string fails validation when FluentValidation's `NotEmpty()` is already doing that — you're testing the framework, not your code
- **EF Core plumbing tests** — `assert dbContext.SaveChanges() returns 1` — tests EF Core, not your logic
- **Getter/setter echoes** — `var job = new Job { Title = "x" }; Assert.Equal("x", job.Title)` — tests the language, not your domain
- **Mock-was-called-ism** — `mockRepo.Verify(r => r.GetByIdAsync(...), Times.Once)` with no assertion about *what the code did with the result*
- **ASP.NET Core framework behavior** — asserting `response.StatusCode == 200` with no assertion about the response body
- **Tautologies** — `result.Total == result.Items.Count` where Total is literally defined as Items.Count
- **Parametrize explosions** — 15 test cases when 2-3 representatives cover the equivalence classes
- **Over-mocked unit tests** — mocking every collaborator so the test verifies your mocks, not your logic
- **Type-system duplication** — `assert result is JobDto` when the signature already guarantees `JobDto`
- **Happy-path duplication** — three near-identical "create job succeeds" tests with minor input variation

## Test Budget

| Change type | Target | Ceiling | Strictness |
|---|---|---|---|
| Single bug fix | Exactly 1 regression test | 1 | **Hard** |
| New command/query handler | 1 happy + 1 validation error + 1 not-found | ≤5 | Soft |
| New API endpoint | 1 integration test (happy) + 1 auth test + 1 validation boundary | ≤5 | Soft |
| New validation rule set | 1 valid + 1 per distinct invalid path | ≤5 | Soft |
| New feature (multi-file) | 3-7 tests total | ≤10 | Soft |
| Refactor with existing coverage | 0 new tests | 0 | **Hard** |

Exceeding a **soft** ceiling: emit `JUSTIFIED: +N tests because [concrete risk tied to a specific failure mode]` in your summary.
Exceeding a **hard** ceiling: do not do it. Escalate instead.

## Preferred Test Types (by priority)

### 1. Integration Tests (highest value)
Use `WebApplicationFactory<Program>` + TestContainers (real database):
```csharp
public class CreateJobTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public async Task CreateJob_WhenRequestIsValid_ReturnsCreatedWithId()
    {
        // Arrange
        var client = factory.CreateAuthenticatedClient();
        var request = new { Title = "Senior Developer", Description = "..." };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/jobs", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JobDto>();
        body!.Title.Should().Be("Senior Developer");
    }
}
```

### 2. Handler Unit Tests (medium value)
Test Application layer handlers with mocked repositories:
```csharp
public class CreateJobHandlerTests
{
    [Fact]
    public async Task Handle_WhenOrgDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var repo = new Mock<IOrganizationRepository>();
        repo.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new CreateJobHandler(repo.Object, /*...*/);

        // Act
        var result = await handler.Handle(
            new CreateJobCommand("title", "desc", Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("organization");
    }
}
```

### 3. Domain Tests (targeted)
Test domain entity invariants directly — no mocks, no EF Core:
```csharp
[Fact]
public void Job_WhenTitleExceedsMaxLength_ThrowsDomainException()
{
    var title = new string('x', 256);
    var act = () => Job.Create(title, "description", Guid.NewGuid());
    act.Should().Throw<DomainException>().WithMessage("*title*");
}
```

## The Delete-First Drill

Before finalizing tests, re-read each test and ask: *"If I deleted this test and ran the suite against a broken version of my implementation, which tests would still fire?"*

Any test that would **not** fire on a realistic break gets deleted.

## Fixture Rules

- **Deterministic** — no `DateTime.Now`, no `Guid.NewGuid()` in assertions (use fixed values)
- **Isolated** — no test-ordering dependencies, no shared mutable state between tests
- **Meaningful assertions** — `result.Should().NotBeNull()` is a placeholder, not an assertion
- **Real cancellation tokens** — use `CancellationToken.None` in tests, not `default`

## Output Expectations

- Report what was tested and what was not.
- Call out residual risk when tests cannot be run.
- If you hit a soft budget ceiling, state the `JUSTIFIED: +N` line in your summary.
- Silent drops for tests that failed the Load-Bearing Filter are fine — no need to report.
