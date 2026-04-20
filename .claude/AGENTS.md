# AGENTS.md — Agent Architecture & Orchestration (.NET Backend)

## Runtime Model Pins

| Agent | Model | Role | Mode |
|---|---|---|---|
| `architect-opus` | Opus | Analysis, architecture, tradeoffs, root cause | Plan (read-only) |
| `builder-sonnet` | Sonnet | .NET implementation, TDD, refactoring | Full access |
| `bug-fixer-sonnet` | Sonnet | Surgical bug fixes | Full access |
| `critic-opus` | Opus | Security, correctness, architecture review | Plan (read-only) |
| `writer-haiku` | Haiku | Documentation, plans, summaries | Full access |

## Stack & Standards

This is a **.NET backend** codebase: C# + ASP.NET Core + Entity Framework Core + Clean Architecture (or Vertical Slice).

- **Implementation standards:** `dotnet-excellence` skill — `.claude/skills/dotnet-excellence/`
  - `CSHARP.md`, `ASPNET.md`, `EF_CORE.md`, `ARCHITECTURE.md`, `SECURITY.md`, `TESTING.md`
- **Verification:** `dotnet build -warnaserror`, `dotnet format --verify-no-changes`, `dotnet test`

`builder-sonnet` implements features following these standards. `critic-opus` reviews against correctness, security, architecture, and .NET-specific antipatterns.

## Orchestration Flow

```
┌─────────────────────────────────────────┐
│            HUMAN OPERATOR               │
│        (Approve / Reject / Clarify)     │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────┐
│      ARCHITECT (Opus)       │
│  RADAR analysis → plan      │
│  Parallelization guidance   │
└──────────────┬──────────────┘
               │ (approval gate)
┌──────────────▼──────────────┐
│    BUILDER(S) (Sonnet)      │
│  .NET + architecture-aware  │
│  TDD: RED → GREEN → REFACTOR│
└──────────────┬──────────────┘
               │ (automatic)
┌──────────────▼──────────────┐
│      CRITIC (Opus)          │
│  Security + correctness     │
│  + architecture audit       │
│  APPROVE / REQUEST CHANGES  │
└──────────────┬──────────────┘
               │ (if issues → fix → re-review)
┌──────────────▼──────────────┐
│      WRITER (Haiku)         │
│  Changelog + docs update    │
└──────────────┬──────────────┘
               │
            Done
```

## Pipeline Commands

| Command | Pipeline | When to Use |
|---|---|---|
| `/feature <desc>` | Architect → Approve → Builder(s) → Critic → Simplify → Writer | New features, multi-file changes |
| `/analyze <problem>` | Architect (RADAR) → Present plan | Architecture decisions, design analysis |
| `/implement <plan>` | Builder(s) parallel → Critic auto-review | Execute an existing plan |
| `/review [files]` | Critic + automated tooling | Pre-merge review |
| `/fix <bug>` | Bug-fixer → Critic → Document | Bug investigation and fix |

## Workflow Patterns

### Pattern A: New Feature (Full Pipeline)
```
/feature "Add job search with filters and pagination"
  → architect-opus analyzes, produces parallelized task plan
  → user approves
  → builder-sonnet(s) implement in parallel groups
  → critic-opus auto-reviews (correctness + security + architecture) → fix loop if needed
  → /simplify checks for unnecessary complexity
  → writer-haiku updates changelog + docs
```

### Pattern B: Bug Fix
```
/fix "Job query returns deleted jobs for admin users"
  → bug-fixer-sonnet writes regression test + minimal fix
  → critic-opus reviews
  → bugs.md updated
```

### Pattern C: Quick Task (Builder Only)
```
Direct request → builder-sonnet implements → critic-opus auto-reviews
```

### Pattern D: Architecture Decision
```
/analyze "CQRS with MediatR vs direct service calls"
  → architect-opus: landscape research, 3 approaches, pairwise comparison
  → recommendation with confidence level + pre-mortem
```

## Escalation Rules

### Builder → Architect
- Plan doesn't match reality (e.g., discovered EF Core limitation)
- Discovered requirement not in spec
- Need new NuGet dependency
- Implementation >50% larger than planned
- EF Core migration would require multi-phase deployment

### Critic → Human
- CRITICAL security vulnerability (SQL injection, auth bypass, token exposure)
- Spec violation
- Architectural deviation (domain logic in API layer, EF Core entities in API responses)
- Breaking API contract change

### Any Agent → Human
- Uncertainty about business rules
- Multiple valid approaches (no clear winner)
- Breaking change to public API contracts
- Database schema change with migration complexity

## Auto-Review Policy
`critic-opus` runs automatically after EVERY implementation. No manual trigger needed. The review loops (fix → re-review) until APPROVE verdict.

## Cost Optimization
- Opus (architect + critic): analysis/planning + review — higher capability where reasoning matters
- Sonnet (builder): primary workhorse — parallel agents for independent tasks
- Haiku (writer): documentation — cheapest for text generation
- Batch critic reviews (all files at once, not per-file)
