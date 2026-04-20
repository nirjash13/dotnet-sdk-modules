# Implementation Plan: [Feature Name]

## Summary
[One paragraph: what is being built and why]

## Architecture Approach
[Clean Architecture | Vertical Slice | other — and why]

## Files to Create/Modify

### New Files
- `src/YourProject.Domain/Entities/[Entity].cs`
- `src/YourProject.Application/[Feature]/Commands/[Command].cs`
- `src/YourProject.Application/[Feature]/Commands/[CommandHandler].cs`
- `src/YourProject.Application/[Feature]/Commands/[CommandValidator].cs`
- `src/YourProject.Infrastructure/Repositories/[Repository].cs`
- `src/YourProject.API/Controllers/[Controller].cs`
- `tests/YourProject.UnitTests/[Feature]/[HandlerTests].cs`
- `tests/YourProject.IntegrationTests/[Feature]/[ApiTests].cs`

### Modified Files
- `src/YourProject.Infrastructure/Persistence/AppDbContext.cs` — add DbSet
- `src/YourProject.Application/Common/Interfaces/IAppDbContext.cs` — add DbSet interface
- `src/YourProject.Infrastructure/DependencyInjection.cs` — register new services

## EF Core Migration
- Migration name: `Add[Entity]Table`
- Changes: [new table, columns, indexes]
- Breaking: yes/no

## Task Groups (for parallel execution)

### Group A — Domain + Application (parallel)
1. Create domain entity with invariants
2. Create command/query DTOs
3. Create FluentValidation validators
4. Write handler unit tests (RED)

### Group B — Infrastructure (after Group A interfaces defined)
5. Create EF Core entity configuration
6. Create repository implementation
7. Add DbContext migration

### Group C — API (after Group A handlers exist)
8. Create controller / endpoint
9. Register DI in DependencyInjection.cs
10. Write integration tests

### Group D — Sequential (after A+B+C)
11. Run all tests, fix failures
12. Update CHANGELOG_AI.md

## Key Interfaces

```csharp
// Defined in Application layer (Group A)
public interface I[Entity]Repository
{
    Task<[Entity]?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<[Entity]Dto>> GetAllAsync(CancellationToken ct);
    Task AddAsync([Entity] entity, CancellationToken ct);
    Task DeleteAsync([Entity] entity, CancellationToken ct);
}
```

## Dependencies
- NuGet packages required: [list any new ones]
- Services to inject: [list]

<!-- written-by: writer-haiku | model: haiku -->
