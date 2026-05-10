# SaaS Builder SDK for .NET 10

A modular .NET 10 SDK for building multi-tenant SaaS products. Pick the modules you need, bring your own where they exist, and ship faster — without giving up the depth (RLS isolation, observability, billing, identity) that production SaaS requires.

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)](https://github.com/your-org/modular-sdk-dotnet/actions)
[![Tests](https://img.shields.io/badge/architecture_tests-6%2F6-brightgreen?style=flat-square)](#testing)
[![NuGet](https://img.shields.io/badge/nuget-preview-blue?style=flat-square)](https://github.com/your-org/modular-sdk-dotnet/packages)
[![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)

> **Status — 2026-05-11.** Phase 1 (SDK extraction & packaging) is complete. Phases 2–5 (Identity/Orgs/RBAC, Tenancy isolation modes, Billing/Entitlements/Feature Flags, cross-cutting modules) are scaffolded with abstractions, default providers, and clearly-marked `TODO(Phase X.Y)` stubs for deferred adapters. See [`docs/TASK_LIST.md`](docs/TASK_LIST.md) for the canonical roadmap and [`docs/USER_GUIDE.md`](docs/USER_GUIDE.md) for the developer journey.

---

## What you get

- **NuGet-distributed core** — `SaasBuilder.SharedKernel`, `SaasBuilder.Persistence`, `SaasBuilder.Host` ship as packages. The Host has zero hard dependencies on any module assembly.
- **Fluent options API** — opt in to exactly the modules and features you need:
  ```csharp
  builder.AddSaasBuilderHost(opts =>
  {
      opts.UseTransport(SaasTransport.InProc);
      opts.UseTenancy(TenantIsolation.PoolWithRls);
      opts.Modules.ScanAssemblyContaining<MyModule>();
      opts.Observability.Enable();
      opts.RateLimiting.UsePerTenantSlidingWindow();
  });
  ```
- **`dotnet new saas-api` template** — `dotnet new install SaasBuilder.Templates && dotnet new saas-api -n MyApp` and you have a runnable starter referencing the SDK as NuGet packages.
- **Multi-tenant by default** — Postgres Row-Level Security + EF Core global query filters + tenant-aware command interceptor; cross-tenant reads return zero rows even when the EF filter is bypassed.
- **Pluggable transport** — MassTransit Mediator (in-process, default) or RabbitMQ Bus (out-of-process). Same handler code; no swap cost.
- **Observability built-in** — OpenTelemetry traces + metrics + structured logs (Serilog) with `tenant_id` enrichment; exports via OTLP to your collector of choice.
- **Standard Webhooks-spec** outbound webhooks (HMAC-SHA256, 5-min replay window, Svix-style retry schedule).
- **OpenFeature-shaped feature flags** with a database default and adapter slots for LaunchDarkly / Unleash / Flagsmith.
- **Entitlements separated from feature flags** — paid gates use `[RequiresEntitlement("advanced_reporting")]`; rollouts use `IFeatureClient`.
- **Cross-cutting modules** — Notifications, Files, Jobs, Audit, Webhooks, Search, Realtime — each silently degrades when its env vars are absent.

---

## Pick & choose modules

The SDK does not force a stack. Common load-outs:

| Load-out | Packages |
|---|---|
| **Minimal** (BYO modules only) | `SaasBuilder.Host` + `SaasBuilder.Persistence` + your `IModuleStartup` |
| **B2B starter** | + `Modules.Identity` + `Modules.Billing` + `Entitlements` + `Modules.Notifications` + `Modules.Audit` + `Modules.Webhooks` |
| **B2C SaaS** | + `Modules.Identity` (single-org mode) + `Modules.Billing` + `FeatureFlags` |
| **Internal tool** | + `Modules.Identity` + `Modules.Audit` (skip billing, notifications, webhooks) |

You can also wrap **existing modules** you already have: implement `IModuleStartup`, mark your tenant-scoped entities with `ITenantScoped`, and register via `opts.Modules.AddType<MyExistingModule>()`. Full walkthrough in [`docs/USER_GUIDE.md` § Bringing your own existing modules](docs/USER_GUIDE.md).

---

## Architecture at a glance

```
                 your code (Program.cs)
                          │
                          ▼
   ┌────────────────────────────────────────────────────┐
   │   SaasBuilder.Host (NuGet)                         │
   │   Composition: AddSaasBuilderHost(opts => …)       │
   │   Pipeline: Auth · Tenant resolver chain · OTel    │
   │             Rate limit · Security headers          │
   │   Transport: MassTransit Mediator OR RabbitMQ Bus  │
   └──────┬──────────────┬──────────────┬───────────────┘
          │              │              │
          ▼              ▼              ▼
    Modules.Identity  Modules.Billing  Modules.Webhooks  …
    Modules.Audit     Modules.Files    your modules
                          │
                          ▼
   ┌────────────────────────────────────────────────────┐
   │   SaasBuilder.Persistence (NuGet)                  │
   │   Multi-tenant DbContext base · RLS interceptor    │
   │   ITenantResources / ITenantResourcesProvider      │
   └──────┬─────────────────────────────────────────────┘
          ▼
   Postgres 16+ (RLS + outbox · pool default)
```

Modules ship as separate NuGet packages following the per-module 5-project layout (`Contracts`, `Domain`, `Application`, `Infrastructure`, `Api`). The Host is a packable library — you reference it as a NuGet, not a project.

---

## Quickstart (10 minutes)

### Prerequisites
- **.NET SDK 10.0.103+** (`dotnet --version`)
- **Docker Desktop** (for Postgres, RabbitMQ, OTel collector)

### Scaffold an app

```bash
# Install the template (one time)
dotnet new install SaasBuilder.Templates

# Scaffold
dotnet new saas-api -n Acme.Saas
cd Acme.Saas

# Bring up dependencies (Postgres, RabbitMQ, OTel collector, Mailhog)
docker compose up -d

# Run
dotnet run
```

Hit `http://localhost:5000/health` — should return `Healthy`.

### Inspect what you got

```
Acme.Saas/
├── Acme.Saas.csproj          # PackageReference SaasBuilder.Host, .Persistence, .SharedKernel
├── Program.cs                # AddSaasBuilderHost(opts => …) fluent setup
├── appsettings.json          # DB connection, OTel endpoint, Auth issuer placeholders
├── docker-compose.yml        # Postgres 16, RabbitMQ 3.13, OTel Collector, Mailhog
├── otel-collector-config.yaml
├── .gitignore  .dockerignore
└── README.md
```

The full developer journey (signup → orgs → SSO → billing → deploy) is in [`docs/USER_GUIDE.md`](docs/USER_GUIDE.md).

---

## Repository layout

```
modular-sdk-dotnet/
├── src/
│   ├── SaasBuilder.SharedKernel/           # Abstractions (netstandard2.0;net10.0)
│   ├── SaasBuilder.Persistence/            # EF Core base + multi-tenant interceptor
│   ├── SaasBuilder.Host/                   # ASP.NET Core composition root (packable library)
│   ├── SaasBuilder.Gateway/                # YARP strangler-fig gateway
│   └── Modules/
│       ├── Identity/                       # OpenIddict + Organizations + RBAC (Phase 2)
│       ├── Ledger/                         # Double-entry accounting (sample/reference)
│       ├── Reporting/                      # Analytics projections (sample/reference)
│       ├── Registration/                   # Saga orchestrator (sample/reference)
│       ├── Billing/                        # IBillingProvider + entitlements (Phase 4)
│       ├── Entitlements/                   # [RequiresEntitlement] (Phase 4)
│       ├── FeatureFlags/                   # OpenFeature-shaped IFeatureClient (Phase 4)
│       ├── Notifications/                  # INotificationDispatcher (Phase 5)
│       ├── Files/                          # IBlobStore (Phase 5)
│       ├── Jobs/                           # IJobScheduler (Phase 5)
│       ├── Audit/                          # IAuditLogger + hash-chain mode (Phase 5)
│       ├── Webhooks/                       # Standard-Webhooks-spec sender (Phase 5)
│       ├── Search/                         # ISearchClient (Phase 5)
│       └── Realtime/                       # SignalR IRealtimeBroadcaster (Phase 5)
├── samples/
│   └── SaasBuilder.Sample.Host/            # Composition root that consumes the SDK
├── templates/
│   └── SaasBuilder.Templates/              # `dotnet new saas-api` template
├── integration/                            # CloudEvents adapter, AsyncAPI registry, .NET 4.8 bridge
├── migrations/                             # SQL migrations per module (RLS policies live here)
├── tests/
│   ├── SaasBuilder.ArchitectureTests/      # NetArchTest layer + RLS coherence rules
│   ├── SaasBuilder.IntegrationTests/       # WebApplicationFactory + Testcontainers
│   ├── SaasBuilder.SecurityTests/          # JWT tampering, rate-limit bypass
│   └── SaasBuilder.SharedKernel.PackageTests/  # Smoke tests against packed nupkg
├── deploy/                                 # OTel Collector, Prometheus, Loki, Tempo, Grafana configs
├── loadtests/                              # NBomber + k6 scenarios + SLO definitions
└── docs/
    ├── USER_GUIDE.md                       # How to use the SDK (journeys, setup, build a SaaS)
    ├── DEPLOYMENT_GUIDE.md                 # How to deploy (Docker / K8s / Azure / AWS / GCP)
    ├── SAAS_SDK_IMPLEMENTATION_PLAN.md     # 10-phase roadmap to v1.0 GA
    ├── TASK_LIST.md                        # Phase-by-phase backlog with status
    └── SAAS_BUILDER_REVIEW.md              # Gap analysis vs peer SaaS builders
```

---

## Key design decisions

1. **MassTransit Mediator over MediatR** — MediatR became commercial in 2024. MassTransit Mediator gives the same API for in-proc dispatch and adds bus-mode parity with no handler changes.

2. **PoolWithRls is the default; other isolation modes are abstracted but stubbed** — `ITenantResources` / `ITenantResourcesProvider` keeps the surface ready for Schema-per-tenant, DB-per-tenant, and Stamp routing without forcing them on the v1 majority.

3. **Modules as NuGet packages, not project references in consumer apps** — the Host is a packable library; consumer apps reference SDK packages and add their own modules either as further packages or as in-repo projects.

4. **Multi-target contracts** — `SaasBuilder.SharedKernel` targets `netstandard2.0;net10.0` so .NET Framework 4.x consumers (legacy estate bridges) can interop without a separate V1/V2 split.

5. **OpenFeature-shaped feature flags, separate from entitlements** — feature flags are for rollouts (percentage, targeting, kill-switch); entitlements gate paid features (boolean or numeric limits). Different concerns, different APIs.

6. **Standard Webhooks spec for outbound** — `webhook-id`, `webhook-timestamp`, `webhook-signature`, 5-minute replay window. Svix-style retry schedule (5s / 5min / 30min / 2h / 5h / 10h / 14h / 20h / 24h).

7. **Idempotency-by-default on jobs, webhooks, billing providers** — every queue/inbound handler requires an idempotency key.

8. **Silent degradation for cross-cutting modules** — if `SENDGRID_API_KEY` isn't set, `Notifications` registers a no-op dispatcher and logs a startup warning. The host doesn't crash; the feature is opt-in.

ADR details and rationale: `.claude/memory/decisions.md`.

---

## Verifying the build

```bash
dotnet restore
dotnet build SaasBuilder.sln -warnaserror     # 0 warnings, 0 errors
dotnet test tests/SaasBuilder.ArchitectureTests/  # 6/6 pass — RLS coherence, layer rules
dotnet pack src/SaasBuilder.Host/SaasBuilder.Host.csproj -c Release -o ./artifacts
```

Phase 5 unit tests (signature/hash-chain/notifications) and Phase 4 unit tests (webhook/subscription/entitlements/feature flags) pass on a developer workstation. Phase 2 + Phase 3 integration tests require Docker (Testcontainers) and run in CI.

---

## Roadmap

| Phase | Scope | Status |
|---|---|---|
| 1 | SDK extraction & packaging | **Complete** |
| 2 | Identity / Organizations / RBAC | Scaffold landed; SAML/SCIM/MFA stubbed |
| 3 | Tenancy isolation modes & lifecycle | Scaffold landed; PoolWithRls real, others stubbed |
| 4 | Billing / Entitlements / Feature Flags | Scaffold landed; Stripe/Paddle/Lemon/Chargebee stubbed |
| 5 | Cross-cutting modules (Notifications, Files, Jobs, Audit, Webhooks, Search, Realtime) | Scaffold landed; cloud adapters stubbed |
| 6 | Admin / Control Plane | Planned |
| 7 | Frontend SDK + starter app | Planned |
| 8 | Compliance (GDPR / SOC 2) + deployment recipes | Planned |
| 9 | Developer experience tooling (`saas` CLI, more templates) | Planned |
| 10 | AI primitives + marketplace + GA | Planned |

See [`docs/SAAS_SDK_IMPLEMENTATION_PLAN.md`](docs/SAAS_SDK_IMPLEMENTATION_PLAN.md) for the full plan and [`docs/TASK_LIST.md`](docs/TASK_LIST.md) for the granular checklist.

---

## Contributing

- Read [`docs/USER_GUIDE.md`](docs/USER_GUIDE.md) and [`ENGINEERS_HANDBOOK.md`](ENGINEERS_HANDBOOK.md) first.
- Layer rules: [`.claude/rules/backend.md`](.claude/rules/backend.md).
- Testing rules: [`.claude/rules/testing.md`](.claude/rules/testing.md) — load-bearing tests only.
- Documentation rules: [`.claude/rules/docs.md`](.claude/rules/docs.md).
- Pre-PR checklist: `dotnet build -warnaserror` + `dotnet test` + new tenant-scoped tables get a CREATE POLICY in `migrations/{module}/*.sql`.

---

## License

**MIT** — see [`LICENSE`](LICENSE).

---

## Acknowledgments

The SaaS Builder SDK draws from:
- Microsoft's [Architecting Multitenant Solutions](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/overview) and AWS [SaaS Lens](https://docs.aws.amazon.com/wellarchitected/latest/saas-lens/welcome.html)
- [OpenFeature](https://openfeature.dev/) (CNCF) for feature-flag shape
- [Standard Webhooks](https://www.standardwebhooks.com/) for outbound webhook spec
- [OWASP ASVS L2](https://owasp.org/www-project-application-security-verification-standard/) for security baseline
- Domain-Driven Design and Clean Architecture
- Peer SaaS builders studied for the gap analysis: ABP Framework, Makerkit, Supastarter, next-forge, SaaS Pegasus, Bullet Train, Frappe/ERPNext
