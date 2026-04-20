Purpose:
Deep RADAR architecture analysis for a .NET design question or decision.

Input:
- `$ARGUMENTS`: Problem, question, or technology to evaluate

Pipeline:

1) ANALYZE (architect-opus)
Spawn architect-opus with the problem and relevant context:
- Problem understanding + scope boundaries
- .NET ecosystem landscape (built-ins first, then NuGet packages)
- 3 distinct approaches (most conventional first)
- Pairwise comparison across: Correctness, Simplicity, Security, Testability, Team Fit, EF Core compatibility, Async behavior
- Recommendation with confidence level
- Pre-mortem: what could go wrong in 6 months?
- Parallelized implementation plan if moving forward

2) PRESENT
Present architect's analysis to user with:
- Recommendation highlighted
- Key tradeoffs summarized
- Migration/rollback considerations for .NET/EF Core changes
- Any VERIFY: items the user should confirm before proceeding

3) DECISION
If user approves, record in `.claude/memory/decisions.md`.
If user chooses different approach, record rationale.
