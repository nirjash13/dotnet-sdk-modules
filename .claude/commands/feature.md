Purpose:
Orchestrate a complete .NET feature implementation: analyze → approve → implement (parallel) → review → document.

Input:
- `$ARGUMENTS`: Feature description or requirement

Pipeline:

1) ANALYZE (architect-opus)
Spawn architect-opus with the feature requirement and project context:
- Load `docs/architecture/system-model.yaml` and `.claude/CHANGELOG_AI.md` as context
- Architect analyzes: problem understanding, .NET landscape research (NuGet-first), 3 approaches, pairwise comparison
- Produces: recommendation, spec, plan, parallelized task list with .NET layer groupings

Present the architect's plan to the user for review.

2) APPROVE
Wait for user approval. If changes requested, re-engage architect-opus with feedback.
Do NOT proceed to implementation without explicit user approval of the plan.

3) IMPLEMENT (builder-sonnet — auto-parallel)
Based on the architect's task groups:
- Identify independent task groups (e.g., Domain entities, Application handlers, Infrastructure repositories, API controllers can often run in parallel)
- Spawn parallel builder-sonnet agents for each independent group
- Each builder follows: scope lock → TDD (RED → GREEN → REFACTOR) → self-verify
- Sequence dependent tasks after prerequisites complete

4) REVIEW (critic-opus — automatic)
After all implementation completes, automatically spawn critic-opus to review ALL changes:
- Security (auth on endpoints, input validation, no secrets), correctness, architecture compliance, EF Core patterns
- Run diagnostic commands: dotnet build -warnaserror, dotnet test, dotnet format --verify-no-changes
- If CRITICAL or HIGH issues found → spawn builder-sonnet to fix → re-review
- Repeat until APPROVE verdict

5) SIMPLIFY
After review passes, run /simplify on the changed code to check for unnecessary complexity.
If simplifications found, apply them and re-run critic review.

6) DOCUMENT (writer-haiku — automatic)
Spawn writer-haiku to:
- Update `.claude/CHANGELOG_AI.md` with changes
- Update architecture docs if contracts, API schema, or EF Core models changed
- Note any new NuGet packages and why they were added

7) SUMMARY
Report to user:
- Files created/modified
- Tests added and status
- Review verdict
- Any new EF Core migrations to review
- Documentation updated
- Any remaining items or follow-ups
