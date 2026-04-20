Purpose:
Run a thorough critic-opus review of changed files or specified files.

Input:
- `$ARGUMENTS`: Optional file paths or "all" for full diff review

Pipeline:

1) SCOPE
Run `git diff --name-status HEAD` to identify changed files.
If $ARGUMENTS specifies files, scope to those.

2) REVIEW (critic-opus)
Spawn critic-opus to review all in-scope files:
- Security: auth on endpoints, input validation, no raw SQL with user input, no secrets
- Correctness: async patterns, null safety, EF Core query correctness
- Architecture: layer boundaries, no entities in API responses, no logic in controllers
- Performance: AsNoTracking on reads, no N+1, CancellationToken propagated
- Type safety: nullable reference types, no ! suppression without comment
- Testing: load-bearing tests only, no ban-list tests

Diagnostic commands:
- `dotnet build -warnaserror`
- `dotnet format --verify-no-changes`
- `dotnet test`

3) VERDICT
If APPROVE → report to user.
If REQUEST CHANGES → list concrete fixes with file:line.
If REJECT → recommend re-architecture, explain why patching won't work.
