# Modular SaaS Chassis for .NET 10

An abstraction-routed hybrid SaaS platform chassis for .NET 10 that unifies modular monolith and microservices architectures—modules run in-process or over a message bus with zero business-code changes.

[![Build Status](https://img.shields.io/badge/build-pending-lightgrey?style=flat-square)](https://github.com/your-org/modular-sdk-dotnet/actions)
[![Test Coverage](https://img.shields.io/badge/coverage-pending-lightgrey?style=flat-square)](https://github.com/your-org/modular-sdk-dotnet/actions)
[![NuGet](https://img.shields.io/badge/nuget-pending-lightgrey?style=flat-square)](https://github.com/your-org/modular-sdk-dotnet/packages)
[![License](https://img.shields.io/badge/license-TBD-blue?style=flat-square)](LICENSE)

---

## What is this?

A .NET 10 foundation for multi-tenant SaaS systems that fuses the rapid iteration of modular monoliths with the scalability of microservices. The chassis enforces:

- **Dynamic module discovery** at startup via `IModuleStartup` abstractions.
- **Dual-mode dispatch** through MassTransit Mediator (in-process) and MassTransit Bus (out-of-process) — toggle the transport per message with zero handler changes.
- **Centralized control plane** — auth, tenancy, logging, rate limiting, and observability flow through the host.
- **Defense-in-depth multi-tenancy** — EF Core global query filters backed by Postgres Row-Level Security.
- **Fault isolation** — Ledger writes complete even if Reporting is offline.

Modules are delivered as both NuGet packages (for external consumption) and project references (for in-repo development).

---

## Problems it solves

| Problem | How the chassis handles it |
|---|---|
| **Modular monolith → microservices migration whiplash** | Start in-process with a single deployment. Flip the transport config to RabbitMQ when load demands. No code changes. |
| **Multi-tenant data isolation risk** | EF Core global filters + Postgres RLS with `FORCE ROW LEVEL SECURITY` — cross-tenant reads return zero rows even if the filter is missing. |
| **Legacy estate absorption (strangler-fig)** | YARP gateway with canary routing. .NET 4.8 bridge via CloudEvents 1.0 + AsyncAPI 3.0. Non-.NET via message contracts. |
| **Dispatcher abstraction sprawl** | Single `IModuleDispatcher` backed by MassTransit end-to-end (vs. MediatR + bus friction). |
| **Observability blackhole** | OpenTelemetry built-in — Prometheus metrics, Loki logs, Tempo traces, Grafana dashboards (all provisioned). No opt-in required. |
| **Mutable licensing dependencies** | Permissively-licensed stack end-to-end — MassTransit 8.x, Marten, YARP, all CQRS machinery. No commercial surprises. |
| **Tenant leakage on new features** | NetArchTest enforces that every tenant-scoped table gets a Postgres RLS policy. Nightly cross-tenant smoke tests. |
| **Boilerplate per new module** | Clean Architecture scaffolding baked in. Shared `ChassisDbContext`, middleware pipeline, pipeline filters. |

---

## Architecture at a glance

```
┌─────────────────────────────────────────────────────┐
│       Chassis.Host  (ASP.NET Core 10)               │
│   Control Plane: Auth · Tenancy · Rate Limit · OTel │
│   Dispatcher: MassTransit (in-memory or RabbitMQ)   │
│   ┌──────────┬────────┬──────────┬──────────────┐   │
│   │ Identity │ Ledger │Reporting │ Registration │   │
│   │(OpenIddir│(EF+Mar.│ (EF Core)│   (Saga)     │   │
│   └──────────┴────────┴──────────┴──────────────┘   │
└──────┬──────────────────┬──────────────┬────────────┘
       ▼                  ▼              ▼
   Postgres 16+      RabbitMQ 3.13  OTel Collector
   (RLS + outbox)    (per-module     (Prom+Loki+Tempo)
                      exchanges)            ↓
                                        Grafana
```

For the full topology diagram and details on tenancy flow, dispatch routing, and integration patterns, see `docs/IMPLEMENTATION_PLAN.md` §3.

---

## Quickstart

### Prerequisites

- **.NET SDK** 10.0.103 or later (`dotnet --version`)
- **Docker** with Docker Compose (for Postgres + RabbitMQ)
- **Optional:** k6 for load testing

### Setup

```bash
# Clone and restore
git clone https://github.com/your-org/modular-sdk-dotnet.git
cd modular-sdk-dotnet
dotnet restore

# Build
dotnet build -warnaserror

# Run tests (unit + integration)
dotnet test

# (Phase 0 status: only SharedKernel is buildable)
# - Chassis.Host arrives in Phase 1
# - Business modules (Identity, Ledger, etc.) arrive in Phases 2–3
# - Full integration testing in Phase 9
```

**Current state:** This is a Phase-0 foundation repository. `Chassis.SharedKernel` is complete and buildable; the Host and modules are stubbed. See the Roadmap section below.

To run the host (once Phase 1 is complete):

```bash
docker-compose -f docker-compose.yml up -d
dotnet run --project src/Chassis.Host
```

---

## Module layout

```
modular-sdk-dotnet/
├── src/
│   ├── Chassis.SharedKernel/           # Abstractions (netstandard2.0;net10.0)
│   ├── Chassis.Host/                   # .NET 10 ASP.NET Core host
│   ├── Chassis.Gateway/                # YARP strangler-fig gateway
│   └── Modules/
│       ├── Identity/                   # OpenIddict auth server (Phase 2)
│       ├── Ledger/                     # Double-entry accounting (Phase 3)
│       ├── Reporting/                  # Analytics projections (Phase 4)
│       └── Registration/               # Saga orchestrator (Phase 5)
├── integration/
│   ├── Integration.Framework48Bridge/  # .NET 4.8 service bridge
│   ├── Integration.CloudEventsAdapter/ # CloudEvents 1.0 envelopes
│   └── Integration.AsyncApiRegistry/   # AsyncAPI 3.0 schema server
├── migrations/                         # SQL migrations per module + RLS templates
├── tests/                              # Architecture + integration + security tests
├── loadtests/                          # k6 scenarios + SLO definitions
└── deploy/                             # OTel Collector, Prometheus, Loki, Tempo, Grafana configs
```

## Key design decisions

1. **MassTransit Mediator, not MediatR** — MediatR moved to commercial license in late 2024. MassTransit Mediator provides the same API for both in-proc dispatch and bus routing, with a single transport-swap configuration toggle.

2. **RLS-only tenancy** — Shared database + shared schema, enforced by Postgres RLS + EF Core global query filters. No pluggable `ITenancyStrategy` in v1.

3. **NuGet + monorepo hybrid** — Modules consumed via project references in the repo (fast iteration) and published as NuGet packages for external consumption (first-class ecosystem member).

4. **Multi-target contracts** — One `*.Contracts` package targeting `netstandard2.0;net10.0`. No V1/V2 split; rich types guarded with `#if NET10_0_OR_GREATER`.

5. **Native AOT: option, not requirement** — The door is kept open via `IModuleLoader` abstraction, but we do not design *for* AOT in v1. See `docs/IMPLEMENTATION_PLAN.md` §10 for the cost analysis.

6. **Marten scoped to audit only** — Event sourcing (Marten) used only in the Ledger module for an append-only `DomainAuditEvent` store. CRUD-heavy modules stay on EF Core.

For ADR details, see `.claude/memory/decisions.md`.

---

## Contributing

- Local dev setup (Docker Compose, environment variables)
- Module scaffolding (Clean Architecture templates)
- Testing standards (load-bearing tests only — see `.claude/rules/testing.md`)
- Code style (C# 12+, nullable reference types enabled, structured logging)
- PR process and pre-merge checklist

Architecture and layer rules are in `.claude/rules/backend.md`. Security rules, database rules, and ASP.NET Core rules are in `.claude/CLAUDE.md`.

---

## License

**TBD** — License has not been finalized. See the project roadmap.

---

## Acknowledgments

This chassis draws architectural guidance from:
- Microsoft's Orleans, YARP, and ASP.NET Core monorepo patterns
- Domain-Driven Design and Clean Architecture principles
