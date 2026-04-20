# Architectural Decisions

<!-- Record durable decisions here. Format: date, decision, rationale, alternatives considered. -->

## Template

### YYYY-MM-DD: [Decision Title]
**Decision:** [What was decided]
**Rationale:** [Why — constraints, requirements, tradeoffs]
**Alternatives considered:** [What else was evaluated]
**Consequences:** [What this means for future code]

---

## Decisions

### 2026-04-20: Approved Approach A+ for Modular SaaS Chassis
**Decision:** Build the hybrid modular chassis on .NET 10 using **Approach A+** — PDF-faithful dual dispatch (in-proc + bus) with two showcase enhancements imported from the contrarian approach: MassTransit EF Core Outbox (required) and Marten event store scoped to the Ledger module's audit trail only.
**Rationale:** Highest score on the pairwise matrix (42/50), best team-fit, direct PDF-spec alignment, smallest risk surface. Approach B (AssemblyLoadContext hot-reload) was Pareto-dominated because runtime hot-reload is out of v1 scope. Approach C (full Wolverine + Marten) lost on team ramp and schedule risk; we imported its best idea (event-sourced audit) into A without paying the full ramp cost.
**Alternatives considered:** Approach B (MT Mediator + ALC plugin isolation, 39/50), Approach C (Wolverine + Marten full event-sourcing, 39/50). See `docs/IMPLEMENTATION_PLAN.md` §3–§4 for the full matrix.
**Consequences:**
- Dispatch unified on **MassTransit Mediator (in-proc) + MassTransit bus (out-of-proc)** — MediatR preemptively rejected on license grounds; see §2.1 of the plan.
- Tenancy is **RLS + EF global filters only** — no pluggable strategy abstraction in v1 (§2.2).
- **Native AOT deferred** but preserved as an option via `IModuleLoader` abstraction; see §10 for the trade-off analysis.
- **Modules shipped as NuGet packages AND consumed via project references** inside the monorepo (§2.4 / §11).
- **Contracts multi-target** `netstandard2.0;net10.0` in one package — no V1/V2 split (§2.5).
- 10-phase implementation plan with parallelized lanes; Phase 0 kicks off next.

---
