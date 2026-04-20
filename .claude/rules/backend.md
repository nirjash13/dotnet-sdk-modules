# Backend Rules (.NET)

## Scope
Applies when editing backend implementation under `src/**`.

## Architecture Standards
- Maintain layer boundaries — no reverse dependencies (API → Application → Domain; Infrastructure implements Application interfaces)
- Domain layer has zero external dependencies (no EF Core, no ASP.NET Core, no NuGet packages)
- Application layer depends only on Domain + abstractions (interfaces, not concrete infrastructure)
- No cross-feature imports except through shared interface contracts

## C# Standards
- Strict nullable reference types enabled — no `!` suppression without a justifying comment
- No `async void` — use `async Task` everywhere except UI event handlers
- No `.Result` or `.Wait()` on Tasks in ASP.NET Core context (deadlock risk)
- Always thread `CancellationToken` through async call chains
- `ConfigureAwait(false)` in Infrastructure and Application layers

## ASP.NET Core Standards
- All endpoints authenticated by default — `[AllowAnonymous]` is opt-out, not opt-in
- No business logic in controllers — only HTTP plumbing (parse request → call handler → format response)
- Request validation via FluentValidation registered in DI
- Global exception middleware handles all unhandled exceptions — no try/catch in controllers
- Return `ProblemDetails` (RFC 7807) for all error responses
- Paginate all list endpoints — never return unbounded collections

## Entity Framework Core Standards
- `AsNoTracking()` on all read-only queries
- Project to DTOs via `.Select()` — never return `DbSet` entities from repositories
- Pass `CancellationToken` to all async EF methods
- `DbContext` accessible only in Infrastructure layer
- Every migration reviewed before applying to staging/production

## Validation at Boundaries
- Validate all external input at the API boundary (FluentValidation)
- Use domain invariants in Domain entities (private setters, constructors with validation)
- Never trust input that crosses a layer boundary without validation

## UX Requirements for APIs
- Every endpoint must return appropriate HTTP status codes (200, 201, 400, 401, 403, 404, 409, 500)
- Error responses include a machine-readable `code` field and human-readable `detail` field
- List endpoints include pagination metadata (`total`, `page`, `pageSize`)
- Long-running operations return 202 Accepted with a status-check location

## Verification
- `dotnet build -warnaserror` — zero errors or warnings
- `dotnet test` — all tests pass
- `dotnet format --verify-no-changes` — code is formatted

## References
- `.claude/CLAUDE.md` for full .NET stack standards
- `.claude/skills/dotnet-excellence/` for C#/ASP.NET Core/EF Core/architecture standards
