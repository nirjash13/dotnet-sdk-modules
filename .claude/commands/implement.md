Purpose:
Execute an existing architect plan using parallel builder agents.

Input:
- `$ARGUMENTS`: Reference to the plan (e.g., "the plan from the feature analysis above")

Pipeline:

1) PARSE PLAN
Read the architect's task groups and identify:
- Independent task groups (can run in parallel)
- Dependent tasks (must sequence after prerequisites)
- .NET-specific parallelization: Domain + Application handlers often parallel; Infrastructure + API after

2) IMPLEMENT (builder-sonnet — parallel)
Spawn parallel builder-sonnet agents per independent task group.
Each builder:
- Locks scope (states what it will and won't do)
- Writes failing test first (RED)
- Implements minimum to pass (GREEN)
- Refactors (REFACTOR)
- Self-verifies: build, test, nullable, async patterns, layer boundaries

Wait for all parallel groups to complete before sequencing dependent tasks.

3) REVIEW (critic-opus — automatic via hook)
After all implementation completes, critic-opus reviews ALL changes together.
Fix loop until APPROVE verdict.

4) REPORT
Files created/modified, tests added, review verdict, EF Core migrations to apply.
