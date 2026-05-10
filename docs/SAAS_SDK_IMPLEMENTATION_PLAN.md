# SaaS-Builder SDK — Implementation Plan (v2)

> **Status:** Revised 2026-05-10 after gap analysis vs peer SaaS-builders (ABP, Makerkit, Supastarter, next-forge, SaaS Pegasus, Bullet Train, Frappe) and industry references (Microsoft Multitenancy guide, Stripe Billing, OpenFeature, OWASP ASVS 5.0, WorkOS/Auth0/Stytch B2B, Standard Webhooks).
>
> **Previous version** (5-week, 5-phase plan limited to packaging + core primitives) was insufficient: it covered Phase 1 packaging, billing/RBAC, DX tooling, Ledger isolation, and marketing — but missed entitlements/feature flags as a separate concern, notifications, file storage, background jobs, outbound webhooks, audit logging, admin/control plane, frontend SDK, compliance (GDPR/SOC2), AI primitives, and isolation-mode flexibility (was RLS-only). This v2 closes those gaps and re-phases the work to be shippable in increments.

---

## 1. Product Vision

**`SaasBuilder`** is a `.NET 10` SDK that lets a small team ship a production-grade B2B/B2C SaaS in **days, not months**, and scale that codebase from modular-monolith to microservices **without rewriting business logic**.

### 1.1 Target Personas

| Persona | Pain solved |
|---|---|
| **Solo founder / 2-person team** | Skip 6+ months of plumbing (auth, tenancy, billing, RBAC, audit, observability) — start on product day 1. |
| **Established .NET shop building a new SaaS** | Adopt opinionated, OSS-friendly stack instead of buying ABP Commercial; keep the option to scale to microservices on the same codebase. |
| **Internal-platform team** | Use as the foundation for a company-wide modular monolith with strong tenant isolation and observability. |

### 1.2 Headline Value Propositions (use in README)

1. **Ship in days, not months.** Auth, multi-tenancy (RLS + envelope encryption), billing, RBAC, observability, audit, and webhooks are done. Focus on your product.
2. **Scale without rewrites.** Start as a single deployable monolith. Flip a switch in `appsettings.json` to extract modules over RabbitMQ/MassTransit when load demands it.
3. **Bulletproof tenant isolation.** Postgres RLS + per-tenant envelope encryption + EF Core query filters + integration tests that *prove* tenant A cannot see tenant B's data — at the database level.
4. **Permissive license, vendor-neutral.** MassTransit + OpenIddict + Serilog + OpenFeature + Stripe abstraction. No commercial-license traps.
5. **Pay only when you grow.** Default integrations (DB-backed feature flags, file-system blob, SMTP) work locally; swap to LaunchDarkly / S3 / SendGrid by setting env vars.

---

## 2. Architecture Principles (load-bearing — do not violate)

1. **Provider-abstracted, default-included.** Every external dependency (email, blob, search, jobs, flags, billing, vector store, LLM) sits behind an `I*` abstraction with a zero-cost default implementation. Consumers swap by env var.
2. **Silent degradation.** A missing optional integration must not break the host — it must register a no-op or in-memory fallback and log a warning at startup. (next-forge pattern.)
3. **Tenant context is ambient and mandatory.** No business code may execute without `ITenantContext`. Cross-tenant ops require an explicit `using (tenantContext.Bypass())` block, fully audited.
4. **Defense in depth on tenant data.** EF Core query filter + Postgres RLS + (Phase 4) per-tenant envelope encryption for PII columns. Each layer assumes the others may be bypassed.
5. **Modules are independently versionable.** Each module is a NuGet package with its own SemVer; consumers choose which to install.
6. **Layered architecture is enforced by tests, not goodwill.** NetArchTest rules in CI prevent drift.
7. **OpenAPI is the contract.** Every endpoint is versioned (`/api/v1`), documented, and produces RFC 7807 ProblemDetails on errors. TS / C# clients are generated from OpenAPI, never hand-written.
8. **Observability per call.** Every handler emits structured logs + traces + RED metrics enriched with `tenant_id`. Per-tenant cardinality is bounded.
9. **AOT-friendly, not AOT-required.** Reflection module loader is replaceable by source-generator-based loader without consumer changes.
10. **Idempotency is the default.** Webhook ingestion, billing sync, and job dispatch all use idempotency keys; retries are safe.

---

## 3. Distribution Model

| Asset | Form | Cadence |
|---|---|---|
| `SaasBuilder.SharedKernel` | NuGet (multi-target `netstandard2.0;net10.0`) | Per release |
| `SaasBuilder.Persistence` | NuGet (`net10.0`) | Per release |
| `SaasBuilder.Host` | NuGet (`net10.0`) — **library, not exe** | Per release |
| `SaasBuilder.Gateway` | OCI image + Helm chart | Per release |
| `SaasBuilder.Modules.Identity` | NuGet | Per release |
| `SaasBuilder.Modules.Billing` | NuGet | Per release |
| `SaasBuilder.Modules.*` (one per module) | NuGet | Per release |
| `SaasBuilder.Templates` | `dotnet new` template package (`saas-api`, `saas-module`) | Per release |
| `SaasBuilder.Cli` | `dotnet tool` (`saas new`, `saas add module`, `saas migrate`, `saas tenant create`) | Per release |
| `@saasbuilder/client` | npm package (TS client codegen runtime) | Per release |
| `saasbuilder/starter-next` | git template repo (Next.js front-end) | Per release |
| Docs site | `docs.saasbuilder.dev` (Docusaurus) | Continuous |

Versions are coordinated under a single SemVer epoch (e.g., `1.0.0`). Breaking changes require a major bump and a migration guide.

---

## 4. Phased Roadmap

The previous plan ran 5 phases over ~8 weeks. This v2 expands to **10 phases over ~26 weeks** (≈6 months) for a v1.0 GA. Phases 2–8 may run partially in parallel once Phase 1 is closed.

### Phase 1 — SDK Extraction & Packaging *(in progress on `feature/phase1-sdk-extraction`)*

**Goal:** Transform the chassis from a monorepo template into reusable, distributable NuGet packages with a fluent options API.

**Status from audit:** ~60% complete. Naming partially renamed; `IsPackable=false` and hardcoded `ProjectReference` to modules in `SaasBuilder.Host.csproj` are blockers; no options pattern; no `dotnet new` template.

**Deliverables:**

1. Rename remaining physical folders `src/Chassis.*` → `src/SaasBuilder.*` and update `.sln` (cosmetic but causes confusion).
2. Make `SaasBuilder.Host` a library (`<OutputType>Library</OutputType>`, `<IsPackable>true</IsPackable>`) — move `Program.cs` into a thin sample under `samples/SaasBuilder.Sample.Host/` that references the package.
3. **Remove** all `<ProjectReference>` from `SaasBuilder.Host.csproj` to module assemblies. Modules must load via reflection from a configured probe path.
4. Replace `AddSaasBuilderHost(this WebApplicationBuilder)` with **fluent options**:
   ```csharp
   builder.AddSaasBuilderHost(opts =>
   {
       opts.UseTransport(SaasTransport.InProc);   // or .Bus
       opts.UseTenancy(TenantIsolation.PoolWithRls);
       opts.Modules.ScanAssemblyContaining<MyModule>();
       opts.Observability.Enable();
       opts.RateLimiting.UsePerTenantSlidingWindow();
   });
   ```
5. Add `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` to `Directory.Build.props` for SDK projects (gated by `IsPackable`).
6. Stand up a minimal `dotnet new saas-api` template that scaffolds an empty host referencing the SDK + a `docker-compose.yml` (Postgres + RabbitMQ + OTel collector + Mailhog).
7. Publish `SaasBuilder.*` packages to GitHub Packages on every `main` push and to NuGet.org on release tag.
8. **Contract test pack (`SaasBuilder.SharedKernel.PackageTests`)** verifying that the package consumed externally compiles + runs a smoke test against a brand-new host.

**Exit criteria:** A developer in a fresh repo runs `dotnet new saas-api -n Acme.Saas`, `dotnet run`, and gets a healthy HTTP server with `/health`, `/openapi`, identity discovery endpoint, and a sample tenant-scoped request that succeeds end-to-end.

---

### Phase 2 — Identity, Organizations & RBAC

**Goal:** Production-grade B2B authentication and authorization aligned with WorkOS/Auth0 Organizations patterns.

**Deliverables:**

1. **Local auth flows:** email/password (Argon2id), email verification, password reset, lockout, magic links. (Already partially in place via OpenIddict — formalize.)
2. **MFA:** TOTP, WebAuthn / passkeys, recovery codes. SMS deferred (regulatory cost).
3. **Social login:** Google, Microsoft, GitHub, Apple via OIDC.
4. **Organizations / Teams** as first-class:
   - `Organization` entity (slug, branding, settings) — distinct from `Tenant` (an Organization may map 1:1 to a tenant in B2B; B2C tenants represent an end-user account).
   - `Member` entity with roles, status (Invited / Active / Suspended).
   - **Optional-teams switch** (Pegasus pattern) — single-user mode for B2C apps.
5. **Invitations** via email/magic link, expiring, role-pre-assigned, with welcome-email template.
6. **RBAC:**
   - Built-in roles: `Owner`, `Admin`, `Member`, `ReadOnly`.
   - Dynamic permission tree (Resource × Action × Scope), seeded per module.
   - `[RequiresPermission("billing.invoice.read")]` ASP.NET Core auth handler.
   - Role-claim enrichment in JWT.
7. **SSO per organization** (B2B):
   - SAML 2.0 + OIDC connection-per-organization.
   - Per-org connection configurator API + admin UI affordance.
8. **SCIM 2.0** inbound provisioning endpoint scoped per organization.
9. **API keys** (user-scoped + org-scoped, hashed at rest, rotation, scopes).
10. **M2M tokens** via OAuth client-credentials with per-app scopes.
11. **Impersonation:** admin-as-user with separate session, banner, time-box, mandatory reason, full audit, optional approval gate (per Pigment "safe impersonation" pattern).
12. **Ownership transfer** + last-owner-protection.
13. **Account deletion** with grace period.

**Exit criteria:** A new tenant can self-onboard, invite a teammate, set up a SAML SSO connection, receive M2M tokens, and rotate an API key. All endpoints have integration tests including cross-tenant isolation assertions.

---

### Phase 3 — Tenancy Enhancements & Lifecycle

**Goal:** Move from "RLS-only" to a full tenancy model that scales from B2C up to enterprise data residency.

**Deliverables:**

1. **Tenant isolation modes** (configurable per deployment):
   - `PoolShared` — single DB, no RLS (B2C personal accounts).
   - `PoolWithRls` *(default — current behavior)* — single DB, Postgres RLS + EF filters.
   - `SiloedSchema` — schema-per-tenant.
   - `SiloedDatabase` — database-per-tenant (premium tier).
   - `SiloedStamp` — full isolated deployment unit (regional / data residency).
2. **Tenant lifecycle state machine:** `Provisioning → Active → Suspended → Archived → Deleted` with hooks (`ITenantLifecycleHandler`).
3. **Tenant resolver pipeline:** ordered, pluggable resolvers (host / path / header / claim / API-key) — replaces today's hardcoded resolver order.
4. **Per-tenant connection strings, blob containers, search indexes** routed through `ITenantResources`.
5. **Per-tenant envelope encryption** (PII columns):
   - `ITenantKeyProvider` (Azure Key Vault / AWS KMS / file dev).
   - EF Core `ValueConverter<EncryptedString>` with tenant-DEK lookup.
6. **Stamp routing** (for `SiloedStamp` mode) — tenant→stamp lookup table + cross-stamp migration tool.
7. **Per-tenant quotas / throttling** (noisy-neighbor isolation) — sliding-window limiter scoped to tenant_id.
8. **Tenant migration runner** — applies pending DDL across pools/silos with leader election + advisory locks.

**Exit criteria:** A consumer can deploy in `PoolWithRls` mode by default, switch one tenant to `SiloedDatabase` for an enterprise customer (via tenant-resources lookup), and prove isolation with a tenant-leak integration test that runs against all four modes.

---

### Phase 4 — Billing, Entitlements & Feature Flags

**Goal:** Monetization is a first-class concern, separate from feature flagging.

**Deliverables:**

1. **`SaasBuilder.Modules.Billing`** with `IBillingProvider` abstraction:
   - Adapters: Stripe (primary), Paddle, Lemon Squeezy, Chargebee.
   - Capabilities: catalog sync, subscription CRUD, **metered usage** ingestion (idempotent), invoices, **customer-portal session creation**, tax integration (Stripe Tax / Avalara).
   - Webhook receiver with **HMAC signature verification + 5-min replay window**.
   - Reconciliation job (DB ↔ provider drift detection — daily).
2. **Plan catalog model** — Products → Prices → **Editions** (named bundles of entitlements, ABP pattern) → Plans. Editions are sold to tenants; entitlements are evaluated against editions.
3. **Per-seat & multi-line billing.** Auto-sync seat count on member add/remove.
4. **Trials** (with/without payment method) + coupons + referral credits.
5. **Soft vs hard limits** — soft limit warns user; hard limit blocks endpoint. Configurable per entitlement.
6. **`SaasBuilder.Entitlements` (entitlements ≠ feature flags):**
   - `IEntitlementService.HasAsync(tenantId, "max_seats")` → boolean / numeric / string value.
   - Entitlements derived from active edition; cached per tenant; invalidated on `subscription.updated`.
   - `[RequiresEntitlement("advanced_reporting")]` ASP.NET Core attribute.
7. **`SaasBuilder.FeatureFlags` (rollout, kill-switch, experimentation):**
   - **OpenFeature**-compatible client (CNCF spec).
   - Default DB-backed provider + adapters for LaunchDarkly, Unleash, Flagsmith, Flagd.
   - Targeting context auto-populated from `ITenantContext`.
8. **Dunning emails** triggered on `invoice.payment_failed` (recovery rate ~20–40%).
9. **Billing dashboard** for tenant admins: invoices, payment methods, plan changes, usage graphs.

**Exit criteria:** A tenant can subscribe via Stripe Checkout, hit `/api/reports/advanced` and get 403 on Free tier, upgrade to Pro and get 200, exceed seat soft limit and see a warning, exceed hard limit and get 402. Webhook replay attack is rejected.

---

### Phase 5 — Cross-cutting Primitives

**Goal:** Ship the universally-required infrastructure modules so consumers don't reinvent them.

**Deliverables:**

1. **`SaasBuilder.Modules.Notifications`**
   - `INotificationDispatcher` (email, in-app, push, SMS, webhook-out).
   - **Email:** abstraction with adapters for SendGrid, AWS SES, Postmark, Resend, Mailgun, SMTP.
   - **Templates:** Razor + MJML; per-tenant branding overrides; localization.
   - **In-app feed** with read state, persisted, pushed via SignalR.
   - **Push** — APNs / FCM adapter.
   - **User preferences** — per-channel opt-in/out per notification type.
   - **Bounce/complaint handling** — provider webhook ingest → suppression list.
2. **`SaasBuilder.Modules.Files`**
   - `IBlobStore` with adapters: FileSystem (dev), Azure Blob, S3, GCS, R2.
   - Per-tenant containers/prefixes + signed URL upload/download.
   - Browser direct-to-storage upload (presigned).
   - Image processing pipeline (ImageSharp): resize / thumbnail / WebP.
   - Quota tracking per tenant.
   - Optional virus scan integration (ClamAV).
3. **`SaasBuilder.Modules.Jobs`**
   - `IJobScheduler` with adapters: Hangfire (default), Quartz.NET, MassTransit scheduled redelivery.
   - Recurring (cron) + delayed + retry-with-backoff + DLQ + replay UI.
   - **Tenant-aware enqueue** — context auto-restored on dequeue.
   - Idempotency key on every job payload.
4. **`SaasBuilder.Modules.Audit`**
   - Centralized append-only audit log (separate from Ledger's domain audit).
   - Schema: actor, tenant, action, resource, before/after, ip, ua, correlation_id.
   - Hash-chain mode for tamper-evidence (SOC 2).
   - GDPR export per tenant (CSV/JSON, time-bounded).
   - SIEM forwarding adapters: Splunk HEC, Datadog, syslog.
   - Retention policy per tenant edition.
5. **`SaasBuilder.Modules.Webhooks`** (outbound)
   - **Standard Webhooks** spec: `webhook-id`, `webhook-timestamp`, `webhook-signature`.
   - HMAC-SHA256 per endpoint; 5-min timestamp window.
   - Subscription manager (tenant-facing UI affordance).
   - Retries: exponential backoff + jitter (Svix-style schedule); DLQ after N failures.
   - Delivery log with replay button.
   - Event registry with JSON schemas.
6. **`SaasBuilder.Modules.Search`**
   - `ISearchClient` adapters: Postgres FTS (default), OpenSearch, Meilisearch, Typesense, Algolia.
   - Per-tenant index or routing key.
   - Indexer pipeline subscribes to domain events.
   - **Query-time tenant scope enforcement** as defense-in-depth.
7. **`SaasBuilder.Modules.Realtime`**
   - SignalR with Redis backplane (default) and SQL backplane option.
   - Tenant-scoped groups auto-join on connect.
   - Presence + broadcast scoping helpers.

**Exit criteria:** Every module has 3 representative integration tests, documentation page, and sample usage in the starter app. Each integration silently degrades when env vars are missing.

---

### Phase 6 — Admin / Control Plane

**Goal:** Give SaaS operators (the people running the SaaS) the tools they need to support customers.

**Deliverables:**

1. **`SaasBuilder.Modules.Admin`** APIs (no UI yet — Phase 7 ships UI):
   - Tenant directory: list, search, filter, drill-in.
   - Tenant inspector: usage, billing, audit, support metadata.
   - Impersonation launcher (calls Phase 2 impersonation endpoint).
   - Feature/entitlement override per tenant.
   - Job dashboard endpoints (list, retry, dead-letter).
   - Webhook delivery dashboard endpoints.
   - Ops health endpoints (DB, queue, providers, SLO status).
   - Feature flag console endpoints.
   - Support actions: resend invite, reset password, refund, credit grant.
   - Approval workflow for sensitive actions (configurable).
2. **Admin authorization model** — separate `SystemAdmin` role, distinct from tenant Owner role; multi-factor required for admin endpoints.

**Exit criteria:** A SaaS operator can resolve a typical support ticket end-to-end from admin APIs (invite expired → resend; subscription stuck → manual sync; user locked out → impersonate to investigate).

---

### Phase 7 — Frontend SDK & Starter App

**Goal:** Backend without a frontend is half a product. Ship a starter that demonstrates every Phase 1–6 capability.

**Deliverables:**

1. **TypeScript client codegen** from OpenAPI (Kiota or NSwag) — published as `@saasbuilder/client` on npm.
2. **Next.js 16 starter app** (`saasbuilder/starter-next` git template):
   - Login (local + social + magic + SSO).
   - MFA setup.
   - Tenant onboarding wizard.
   - Member management + invitations.
   - Billing portal + Stripe checkout.
   - In-app notifications feed.
   - Webhook subscription UI.
   - File upload demo.
   - Theme system with per-tenant branding.
3. **Blazor WASM starter** (parity with Next.js for .NET shops).
4. **Hosted UI pages** (drop-in MVC pages for login, MFA setup, billing portal, accept-invitation) — for backend-only consumers.
5. **Admin UI** (a separate Next.js app under same template) consuming Phase 6 APIs.
6. **Webhook subscription manager** as a reusable React component.

**Exit criteria:** Starter app deploys to Vercel + Azure App Service in < 30 minutes. Lighthouse 90+ on landing page. Demonstrates billing, RBAC, MFA, file upload, real-time notifications.

---

### Phase 8 — Compliance & Deployment

**Goal:** Make compliance and production deployment a checkbox, not a project.

**Deliverables:**

1. **`SaasBuilder.Modules.Gdpr`** — personal data export (zip), erasure (right-to-be-forgotten) with grace period, consent management, sub-processor list generator, DPA template.
2. **Encryption at rest** integration guidance — DB TDE + Phase 3 envelope encryption.
3. **Data residency** patterns — `SiloedStamp` mode + region pinning per tenant.
4. **SOC 2 audit-trail mode** — hash-chained audit log + retention guarantee + access review report.
5. **Helm chart** for Kubernetes deployment with HPA + PodDisruptionBudgets.
6. **IaC samples** — Bicep + Terraform for Azure stamps, Terraform for AWS stamps.
7. **Blue/green & canary** deployment recipes (Argo Rollouts, Azure deployment slots).
8. **Migration runner** with leader election + advisory locks; expand-migrate-contract guide for zero-downtime schema changes.
9. **Health probes** — `/health/live`, `/health/ready`, `/health/startup` distinct.
10. **Backup & restore** — per-tenant point-in-time-restore tooling.

**Exit criteria:** A consumer can deploy to AKS or EKS with Helm, satisfy SOC 2 audit-log requirements out of the box, and produce a GDPR data-export within hours of request.

---

### Phase 9 — Developer Experience & Tooling

**Goal:** Cut "first PR" time from days to hours.

**Deliverables:**

1. **`SaasBuilder.Cli` (`dotnet tool`):**
   - `saas new <name>` — scaffold a new SaaS app.
   - `saas add module <name>` — scaffold a new vertical-slice module (Contracts/Domain/Application/Infrastructure/Api projects + DbContext + RLS migration template + endpoint stub + tests).
   - `saas add feature <module> <name>` — scaffold an endpoint + DTO + validator + handler + permission seed + OpenAPI fragment + integration test (Bullet Train "super-scaffolding" pattern).
   - `saas migrate` — apply pending migrations across modules in dependency order.
   - `saas tenant create <slug>` — provision a tenant locally (set up RLS row, seed roles).
   - `saas pack` — build all SDK packages with consistent versioning.
2. **`dotnet new` template package** — `saas-api`, `saas-module`, `saas-feature` templates.
3. **Sample apps** under `samples/`:
   - `samples/B2BSample` (multi-tenant, SAML SSO, billing, RBAC).
   - `samples/B2CSample` (single-user-per-tenant, magic link, simple billing).
   - `samples/MarketplaceSample` (extension surface demonstrating Phase 10).
4. **Docs site** at `docs.saasbuilder.dev` (Docusaurus or VitePress):
   - "Build your first SaaS in 30 minutes" tutorial.
   - "Add a custom module" tutorial.
   - "Configure Stripe billing" tutorial.
   - "Configure SAML SSO for an enterprise customer" tutorial.
   - "Deploy to AKS / EKS / App Service" tutorials.
   - Architectural decision records (ADRs) — versioned.
   - API reference auto-generated from OpenAPI.
5. **Scaffolding generators** must emit endpoint + DTO + validator + handler + tenant filter + permission seed + OpenAPI doc + integration test in one command.
6. **Aspire AppHost integration** sample for orchestrated local dev (Postgres + Redis + Rabbit + Mailhog + Azurite + OTel collector).

**Exit criteria:** A new developer follows the docs and builds a "to-do list SaaS" with tenants, billing, SSO, and an admin page in under one workday.

---

### Phase 10 — AI Primitives, Marketplace, GA Launch

**Goal:** Position for the 2026 expectation that every SaaS has AI features and an extension marketplace.

**Deliverables:**

1. **`SaasBuilder.Modules.Ai`**
   - `ILlmClient` over `Microsoft.Extensions.AI` (OpenAI / Anthropic / Azure OpenAI / Bedrock / Google / Ollama).
   - `IEmbeddingClient` + vector-store abstraction (pgvector default, Qdrant, Pinecone, Azure AI Search).
   - **RAG pipeline** with **mandatory tenant scope** on retrieval (a missing WHERE clause leaks data — this is enforced by tests).
   - Prompt safety: input sanitization, output validation, jailbreak detection, PII redaction.
   - **Per-call usage capture** (model, tokens, cost) → audit + metering pipelines.
   - Per-tenant LLM budget with soft + hard caps.
   - Streaming via SSE with cancellation.
   - Tool use / function calling abstraction.
   - Evaluation harness — golden-set tests, regression detection, prompt versioning.
   - **MCP server adapter** — expose tenant data as a Model Context Protocol server scoped to caller's tenant.
2. **`SaasBuilder.Modules.Marketplace`**
   - Module manifest registry — capabilities, deps, signed.
   - **OAuth-app surface** — third parties register apps that act on tenant behalf (Slack-app pattern).
   - Webhook + REST + UI extension points for installed apps.
   - Per-tenant install/uninstall flow with admin approval.
   - Permission scopes an installed app requests; tenant admin grants.
   - Optional revenue-share via Phase 4 billing.
3. **GA Launch:**
   - Performance benchmark: 1000 RPS sustained on a 4-vCPU host (load tests already in `loadtests/nbomber/`).
   - Security audit (external pen test recommended).
   - Versioning policy + LTS commitment for v1.x.
   - Migration guide from peer frameworks (ABP → SaasBuilder, plain ASP.NET → SaasBuilder).
   - README overhaul with case studies.
   - Public launch post.

**Exit criteria:** v1.0.0 packages on NuGet.org; two reference customers in production; documented LTS policy.

---

## 5. Module Catalog (Final State, post-Phase 10)

```
Core (always-on):
  SaasBuilder.SharedKernel
  SaasBuilder.Persistence
  SaasBuilder.Host
  SaasBuilder.Gateway
  SaasBuilder.Tenancy             (extracted from SharedKernel in Phase 3)

Identity layer:
  SaasBuilder.Modules.Identity    (auth + orgs + RBAC + invites + impersonation)
  SaasBuilder.Modules.Sso         (SAML/OIDC connection-per-org, optional)
  SaasBuilder.Modules.Scim        (inbound provisioning, optional)

Monetization layer:
  SaasBuilder.Modules.Billing
  SaasBuilder.Entitlements
  SaasBuilder.FeatureFlags

Cross-cutting layer:
  SaasBuilder.Modules.Notifications
  SaasBuilder.Modules.Files
  SaasBuilder.Modules.Jobs
  SaasBuilder.Modules.Audit
  SaasBuilder.Modules.Webhooks
  SaasBuilder.Modules.Search
  SaasBuilder.Modules.Realtime

Operations layer:
  SaasBuilder.Modules.Admin

Compliance layer:
  SaasBuilder.Modules.Gdpr

Optional / examples (NOT in core):
  SaasBuilder.Modules.Ledger      (fintech-specific, demo only)
  SaasBuilder.Modules.Reporting   (read-model demo)
  SaasBuilder.Modules.Registration (saga demo)

Modern primitives:
  SaasBuilder.Modules.Ai
  SaasBuilder.Modules.Marketplace

Developer Experience:
  SaasBuilder.Cli                 (dotnet tool)
  SaasBuilder.Templates           (dotnet new templates)
```

The current `Modules/Ledger`, `Modules/Reporting`, `Modules/Registration` move to a `samples/` or `examples/` folder — they are valuable demos but should not pollute the core SDK.

---

## 6. Architecture Decisions Carried Over from v1

These decisions remain valid and are not revisited:

- **Dispatcher** — MassTransit Mediator (in-proc) + MassTransit Bus (out-of-proc) over MediatR (commercial license).
- **Multi-tenancy default** — Postgres RLS + EF Core query filters (Phase 3 adds the other isolation modes).
- **AOT-friendly, not AOT-required** — preserve the option without paying the cost today.
- **Module delivery** — NuGet + monorepo hybrid.
- **Contracts packaging** — single multi-target package per module (no V1/V2 split).
- **MassTransit EF Core Outbox** — required for out-of-proc correctness.
- **Marten** — used only in Ledger (moves to samples).
- **YARP** — gateway with canary routing.
- **NetArchTest** — enforce layer boundaries.

---

## 7. Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| Scope sprawl — 10 phases of new modules | High | Each phase ships independently; partial adoption is supported (consumers install only the modules they need). |
| Provider abstractions leak provider semantics | Medium | Treat provider adapters as adapters, not facades; minimize abstraction surface to lowest-common-denominator capabilities; document provider-specific extensions explicitly. |
| Versioning hell across 15+ packages | Medium | Single SemVer epoch coordinated by `SaasBuilder.Cli pack`; integration tests assert that all `1.x.y` packages compose. |
| Phase 4 billing complexity (tax, dunning, proration) | High | Stripe is the primary adapter; other providers ship with documented capability matrix; consumers pick provider that meets their needs. |
| Phase 3 tenant migration between isolation modes | High | Tenant migration is explicitly out of scope for v1.0 — declared as future work; consumers pick mode at provisioning time. |
| Phase 10 AI primitives: tenant data leakage in RAG | High | RAG pipeline ships with mandatory tenant filter on retrieval; integration tests assert cross-tenant retrieval returns zero rows; this is treated as a security invariant, not a feature. |
| Frontend starter goes stale faster than backend | Medium | Frontend starter is a separate repo with its own release cadence; tied to backend via OpenAPI version compatibility tests. |

---

## 8. Immediate Next Steps

Phase 1 is in flight. To unblock everything else:

1. Make `SaasBuilder.Host` packable and remove module ProjectReferences (audit shows this is the blocker). [≤ 1 day]
2. Refactor `AddSaasBuilderHost` to take an options action. [≤ 1 day]
3. Stand up `dotnet new saas-api` skeleton. [≤ 1 day]
4. Publish first preview packages to GitHub Packages. [≤ 0.5 day]
5. Begin Phase 2 (Identity / Orgs / RBAC) in parallel with Phase 3 (Tenancy modes) on separate branches.

The detailed actionable backlog lives in `docs/TASK_LIST.md`.
