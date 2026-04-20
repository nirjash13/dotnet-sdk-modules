Purpose:
Orchestrate a .NET bug fix: diagnose → fix → review → document.

Input:
- `$ARGUMENTS`: Bug description or symptom

Pipeline:

1) DIAGNOSE
Read the relevant code. Common .NET root causes to check first:
- Async: `.Result`/`.Wait()` deadlock, `async void`, missing `await`, fire-and-forget
- EF Core: N+1 query, missing `AsNoTracking()`, tracking causing unexpected updates, missing null check on `FirstOrDefault()`
- Nullable: null reference from unguarded `FirstOrDefault()` / `SingleOrDefault()`
- Cancellation: `CancellationToken` accepted but not forwarded to EF/HTTP calls
- Auth: missing `[Authorize]`, wrong policy, claims not extracted correctly

State the root cause and fix approach before implementing.

2) FIX (bug-fixer-sonnet)
Spawn bug-fixer-sonnet with:
- Confirmed root cause
- Exact file:line of the bug
- Fix approach

Bug-fixer writes exactly ONE regression test (hard ceiling) then applies minimal fix.

3) REVIEW (critic-opus — automatic via hook)
Critic reviews:
- Correctness of the fix
- Whether the regression test would actually catch the bug
- No new issues introduced
- dotnet build -warnaserror passes

If REQUEST CHANGES → bug-fixer-sonnet fixes → re-review until APPROVE.

4) DOCUMENT (writer-haiku)
Record in `.claude/memory/bugs.md`:
```
BUG: [title]
SYMPTOM: [what was observed]
ROOT CAUSE: [exact cause — file:line]
FIX: [what changed and why]
FILES: [modified files]
TEST: [regression test name + location]
PREVENTION: [what prevents recurrence]
```
