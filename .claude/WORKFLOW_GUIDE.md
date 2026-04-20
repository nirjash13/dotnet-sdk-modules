# .claude Workflow Guide (.NET Backend)

## Goal
Use the `.claude` folder as an operating system for AI-assisted .NET development:
- Automated planning/build/review pipelines
- Persistent memory between sessions
- Up-to-date architecture + changelog context
- Specialized agents with model-pinned roles

---

## Agents

| Agent | Model | Purpose |
|---|---|---|
| `architect-opus` | Opus | Analysis, architecture, RADAR methodology, .NET patterns |
| `builder-sonnet` | Sonnet | .NET implementation, TDD, async patterns, EF Core |
| `bug-fixer-sonnet` | Sonnet | Surgical .NET bug fixes |
| `critic-opus` | Opus | Security, correctness, architecture review (auto after every impl) |
| `writer-haiku` | Haiku | Documentation, plans, changelogs |

## Pipeline Commands (Primary Workflow)

| Command | What It Does |
|---|---|
| `/feature <desc>` | Full pipeline: analyze → approve → implement (parallel) → review → simplify → docs |
| `/analyze <problem>` | Deep RADAR analysis → recommendation + plan |
| `/implement <plan>` | Auto-parallel builders → auto-review |
| `/review [files]` | Critic review + automated tooling |
| `/fix <bug>` | Diagnose → fix → review → document |

### Utility Commands

| Command | What It Does |
|---|---|
| `/add <files>` | Load files as mandatory context |
| `/update-summary` | Update architecture docs + CHANGELOG_AI.md |
| `/update-memory` | Update all memory files |
| `/record-decision` | Record architectural decision |
| `/record-bug` | Record bug fix |
| `/record-pattern` | Record reusable pattern |
| `/session-summary` | End-of-session summary |
| `/verify-writing` | Check writer-haiku provenance markers |

---

## Daily Workflow

### Session Start
1. Context auto-loads: `active.yaml`, core rules.
2. If docs lag behind code, run `/update-summary`.
3. Decide your path:
   - New feature → `/feature`
   - Bug → `/fix`
   - Architecture question → `/analyze`
   - Quick change → just describe it (builder + auto-review)

### During Work
- Pipeline commands handle orchestration — no need to manually invoke each agent.
- Architect decides parallelization. Builder agents run in parallel for independent tasks.
- Critic auto-reviews after every implementation.
- Record decisions/bugs/patterns as they happen.

### Session End
- Run `/update-memory` to persist learnings.
- Run `/update-summary` if architecture/contracts changed.

---

## Key Files

### Core Contracts
- `CLAUDE.md` (root): .NET stack, commands, standards
- `.claude/AGENTS.md`: Agent architecture, orchestration patterns, escalation rules
- `.claude/constitution.md`: Immutable guardrails (security, testing, deployment)
- `.claude/skills/dotnet-excellence/`: C#/ASP.NET Core/EF Core/architecture/security standards

### Agents (`.claude/agents/`)
Each file defines a model-pinned specialist with embedded instructions.

### Memory System
- `.claude/memory/active.yaml`: Always-loaded compact memory
- `.claude/memory/context.md`: Expanded session context
- `.claude/memory/decisions.md`: Architectural decisions
- `.claude/memory/bugs.md`: Bug fixes with root causes
- `.claude/memory/patterns.md`: Reusable patterns

---

## End-to-End Playbooks

### New Feature
```
/feature "Add job search with full-text search and filters"
```
Pipeline: analysis → plan → approval gate → parallel implementation → review loop → simplify → docs.

### Bug Fix
```
/fix "GetJobsAsync returns soft-deleted jobs when called by admin"
```
Pipeline: regression test → fix → review → bugs.md.

### Architecture Decision
```
/analyze "CQRS with MediatR vs direct service injection"
```
Architect produces: landscape research, 3 approaches, pairwise comparison, recommendation with pre-mortem.

### Quick Task
```
"Add pagination to the GET /api/v1/jobs endpoint"
```
Builder implements with auto-review.

---

## Quality Rules (Non-Negotiable)
- Respect `.claude/constitution.md`.
- Tests required for behavior changes (TDD).
- Critic review after every implementation.
- No new NuGet packages without verification (maintained? compatible? license?).
- Architecture/changelog updated after significant changes.
- EF Core migrations reviewed before applying.
- Security first: `[Authorize]` on endpoints, secrets in env vars, `[AllowAnonymous]` is explicit opt-out.
