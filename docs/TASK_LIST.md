# SaaS-Builder SDK — Phased Task List (v2)

> **Status:** Revised 2026-05-10. Tracks the 10-phase roadmap in `SAAS_SDK_IMPLEMENTATION_PLAN.md` (v2). Phase 1 is in flight on `feature/phase1-sdk-extraction`. Phases 2–8 may run partially in parallel after Phase 1 closes; Phases 9–10 sequence after the core is stable.
>
> **Conventions:**
> - `[x]` complete, `[~]` partial / in-progress, `[ ]` not started
> - Each phase has an **Exit gate** that blocks the next phase from being declared "Done"
> - File-path references use `path:line` format where applicable
> - Subagents/agents that own a task are noted in `« »`

---

## Phase 1 — SDK Extraction & Packaging

**Goal:** Reusable, distributable NuGet packages with a fluent options API.
**Branch:** `feature/phase1-sdk-extraction`

### 1.1 Naming consistency
- [~] Rename `src/Chassis.Host/` folder → `src/SaasBuilder.Host/` (assembly already renamed; folder lags)
- [~] Rename `src/Chassis.SharedKernel/`, `src/Chassis.Persistence/`, `src/Chassis.Gateway/` folders to match assembly names
- [ ] Rename `tests/Chassis.*/` folders → `tests/SaasBuilder.*/` (one new pair already exists side-by-side; consolidate)
- [ ] Update `SaasBuilder.sln` to reflect renamed paths; remove duplicate solution items
- [ ] Update all `using Chassis.*` and `namespace Chassis.*` to `SaasBuilder.*` across `src/`, `tests/`, `integration/`
- [ ] Update `Directory.Build.props` paths if any reference old folder names

### 1.2 Decouple Host from Modules (BLOCKER for SDK distribution)
- [ ] Set `SaasBuilder.Host.csproj` to `<OutputType>Library</OutputType>` and `<IsPackable>true</IsPackable>`
- [ ] **Remove** every `<ProjectReference>` from `SaasBuilder.Host.csproj` to `Modules/*` projects (Identity, Ledger, Reporting, Registration)
- [ ] Move existing `src/Chassis.Host/Program.cs` to `samples/SaasBuilder.Sample.Host/Program.cs`; the sample references the package, not the source
- [ ] Replace `AddSaasBuilderHost(this WebApplicationBuilder)` with fluent options:
      ```csharp
      builder.AddSaasBuilderHost(opts => {
          opts.UseTransport(SaasTransport.InProc);
          opts.UseTenancy(TenantIsolation.PoolWithRls);
          opts.Modules.ScanAssemblyContaining<MyModule>();
          opts.Observability.Enable();
          opts.RateLimiting.UsePerTenantSlidingWindow();
      });
      ```
- [ ] Implement `SaasBuilderOptions`, `SaasBuilderModulesOptions`, `SaasBuilderObservabilityOptions`, `SaasBuilderTransportOptions`
- [ ] `ReflectionModuleLoader` reads probe paths from options (not hardcoded `AppDomain.BaseDirectory + "modules/"`); supports assembly globs
- [ ] Update `samples/SaasBuilder.Sample.Host` to load Identity, Ledger, etc. from a configured probe folder

### 1.3 Packaging metadata
- [ ] Add `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` to `Directory.Build.props` (gated by `IsPackable`)
- [ ] Confirm every SDK `.csproj` has: `PackageId`, `Description`, `Authors`, `Company`, `License`, `RepositoryUrl`, `Tags` — currently OK on `SaasBuilder.SharedKernel.csproj`, missing on `SaasBuilder.Host.csproj` after switch to library
- [ ] Add `README.md` per package (NuGet displays it)
- [ ] Add `icon.png` referenced via `<PackageIcon>` for branding consistency
- [ ] Confirm `MinVer` tag-driven versioning works for all SDK projects
- [ ] Multi-target `SaasBuilder.SharedKernel` to `netstandard2.0;net10.0` (audit confirms only net10.0 today)

### 1.4 First-class `dotnet new` template (skeleton in Phase 1; extends in Phase 9)
- [ ] Create `templates/SaasBuilder.Templates/` project with `<PackageType>Template</PackageType>`
- [ ] Implement `saas-api` template: scaffolds `Program.cs`, `appsettings.json`, `docker-compose.yml`, `.gitignore`, `README.md` referencing the SDK packages
- [ ] `docker-compose.yml` includes Postgres, RabbitMQ, OTel collector, Mailhog
- [ ] Smoke test: `dotnet new saas-api -n Smoke && cd Smoke && dotnet build && dotnet run` — host starts and responds to `/health`

### 1.5 CI / publishing
- [ ] Create `.github/workflows/publish-nuget.yml` (or rename existing `pack-and-publish.yml`)
- [ ] Trigger on release tags; manual `workflow_dispatch` for previews
- [ ] Publish all `SaasBuilder.*` packages to GitHub Packages on `main` push (preview)
- [ ] Publish to NuGet.org on release tags (`v*.*.*`); requires `NUGET_API_KEY` secret
- [ ] CI gate: `dotnet pack` must succeed before publish

### 1.6 Contract / smoke tests for consumers
- [ ] `tests/SaasBuilder.SharedKernel.PackageTests` consumes the **packaged** NuGet (not project reference) and asserts public API surface
- [ ] Add `tests/SaasBuilder.Host.PackageTests` doing the same for the Host package after 1.2
- [ ] Add CI step that runs against the locally-packed `.nupkg` artifacts before publish

### 1.7 Verification
- [ ] `dotnet build -warnaserror` passes with zero warnings
- [ ] `dotnet test` passes including architecture and integration tests
- [ ] `dotnet pack` produces `SaasBuilder.SharedKernel.X.Y.Z.nupkg`, `SaasBuilder.Persistence.X.Y.Z.nupkg`, `SaasBuilder.Host.X.Y.Z.nupkg`
- [ ] Sample host runs from packaged NuGet (not project references)

### Phase 1 Exit Gate
A developer in a fresh repo runs `dotnet new saas-api -n Acme.Saas`, `dotnet run`, hits `/health` and a tenant-scoped sample endpoint, and the run succeeds end-to-end. The Host has zero `<ProjectReference>` to module assemblies.

---

## Phase 2 — Identity, Organizations & RBAC

**Goal:** Production-grade B2B authentication and authorization aligned with WorkOS / Auth0 Organizations.

### 2.1 Local auth flow hardening
- [ ] Email/password with **Argon2id** (not PBKDF2) — confirm or upgrade existing OpenIddict storage
- [ ] Email verification flow with token expiry + reuse protection
- [ ] Password reset via email magic link
- [ ] Account lockout after N failed attempts; admin unlock endpoint
- [ ] Magic-link sign-in (separate flow from password reset)

### 2.2 MFA
- [ ] TOTP enrollment + verification flow
- [ ] WebAuthn / passkeys registration + authentication
- [ ] Recovery codes (10 codes, single-use, hashed at rest)
- [ ] Admin "force MFA" policy per organization

### 2.3 Social login
- [ ] OIDC adapter for Google
- [ ] OIDC adapter for Microsoft
- [ ] OIDC adapter for GitHub
- [ ] OIDC adapter for Apple
- [ ] Account-linking flow (existing user adds social provider)

### 2.4 Organizations / Teams
- [ ] `Organization` entity (slug, name, branding, settings) — distinct from `Tenant`
- [ ] `Member` entity with role + status (Invited / Active / Suspended)
- [ ] **Optional-teams switch** (Pegasus pattern) — runtime config flag for B2C apps
- [ ] Organization CRUD endpoints
- [ ] Member CRUD endpoints with last-owner-protection invariant
- [ ] Ownership transfer flow with confirmation email
- [ ] Domain-claimed orgs — auto-join verified email domain (optional flag)

### 2.5 Invitations
- [ ] Invite-by-email with role-pre-assigned + magic link
- [ ] Invitation expiry (default 7 days)
- [ ] Resend invite endpoint
- [ ] Revoke invite endpoint
- [ ] Welcome email template (uses Phase 5 notification module; Phase 2 ships a minimal SMTP fallback)

### 2.6 RBAC
- [ ] Built-in roles seeded: `Owner`, `Admin`, `Member`, `ReadOnly`
- [ ] Dynamic permission tree (Resource × Action × Scope) — `IPermissionRegistry`
- [ ] Per-module permission seed via `IPermissionDefinitionProvider`
- [ ] `[RequiresPermission("billing.invoice.read")]` ASP.NET Core auth handler
- [ ] Role-claim enrichment in JWT (extend existing `TenantClaimEnricher`)
- [ ] Permission-check audit (every denied check logged)

### 2.7 SSO per organization (B2B)
- [ ] SAML 2.0 connection-per-organization (use `Sustainsys.Saml2` or `ITfoxtec.Identity.Saml2`)
- [ ] OIDC connection-per-organization
- [ ] Per-org connection configurator API
- [ ] IdP-initiated and SP-initiated flows
- [ ] Just-in-time user provisioning on first SSO login
- [ ] Connection metadata download endpoint (XML for SAML)

### 2.8 SCIM 2.0 inbound provisioning
- [ ] `/scim/v2/Users` endpoint scoped per-org
- [ ] `/scim/v2/Groups` endpoint scoped per-org
- [ ] Bearer-token auth per-org SCIM endpoint
- [ ] Conformance test pack (Okta + Microsoft Entra)

### 2.9 API keys & M2M tokens
- [ ] User-scoped API keys with scopes; hashed at rest; rotation endpoint
- [ ] Org-scoped API keys
- [ ] M2M OAuth client_credentials flow with per-app scopes
- [ ] Authorization handlers that accept either JWT, API key, or M2M token

### 2.10 Impersonation (per Pigment "safe impersonation" pattern)
- [ ] System-admin-only endpoint to start impersonation
- [ ] Mandatory reason field; optional approval gate
- [ ] Time-boxed session (max 1h, configurable)
- [ ] Banner UI affordance (frontend) — header `X-Impersonation: true`
- [ ] Full audit trail: actor (admin), target (user), reason, start/end, every action
- [ ] End-impersonation endpoint

### 2.11 Account lifecycle
- [ ] Account deletion with grace period (30 days)
- [ ] Restore-deleted-account endpoint within grace period
- [ ] Hard-delete cron after grace period

### Phase 2 Exit Gate
A new tenant self-onboards, invites a teammate, sets up SAML SSO, mints an M2M token, rotates an API key. Cross-tenant isolation integration tests pass for every new endpoint.

---

## Phase 3 — Tenancy Enhancements & Lifecycle

**Goal:** Move from "RLS-only" to a configurable isolation model that scales from B2C to enterprise data residency.

### 3.1 Isolation modes
- [ ] `TenantIsolation` enum: `PoolShared`, `PoolWithRls`, `SiloedSchema`, `SiloedDatabase`, `SiloedStamp`
- [ ] `ITenantResources` abstraction → connection string, blob container, search index per tenant
- [ ] `PoolWithRls` (today's behavior) refactored to implement `ITenantResources`
- [ ] `SiloedSchema` mode: schema-per-tenant; EF migrations targeted per schema
- [ ] `SiloedDatabase` mode: connection-string lookup; migration runner per DB
- [ ] `SiloedStamp` mode: regional deployment; `IStampRouter` to map tenant → stamp URL
- [ ] Deployment recipe per mode in docs

### 3.2 Tenant lifecycle state machine
- [ ] `TenantStatus` enum: `Provisioning`, `Active`, `Suspended`, `Archived`, `Deleted`
- [ ] `ITenantLifecycleHandler` hooks (OnProvision, OnSuspend, OnArchive, OnDelete)
- [ ] Provisioning workflow: create resources, seed roles, send welcome
- [ ] Suspension workflow: block writes, return 402 / 423; preserve reads for billing
- [ ] Archive workflow: data export + read-only mode
- [ ] Delete workflow: hard delete after retention period

### 3.3 Tenant resolver pipeline
- [ ] `ITenantResolver` interface with priority
- [ ] Built-in resolvers: `JwtClaim`, `Header`, `Subdomain`, `Path`, `ApiKey`
- [ ] Pipeline configurable via options: `opts.Tenancy.Resolvers.Add<JwtClaim>().Then<Subdomain>()`
- [ ] Falls back to anonymous-bypass for `/health`, `/openapi`, `/.well-known`, `/connect`

### 3.4 Per-tenant envelope encryption (PII columns)
- [ ] `ITenantKeyProvider` abstraction
- [ ] Adapters: Azure Key Vault, AWS KMS, Google Cloud KMS, FileSystem (dev only)
- [ ] DEK caching with TTL; KEK rotation hook
- [ ] EF Core `ValueConverter<EncryptedString>` and `ValueConverter<EncryptedBytes>`
- [ ] `[Encrypted]` attribute on entity properties for declarative use
- [ ] Migration playbook (re-encrypt on rotation)

### 3.5 Per-tenant throttling / quotas
- [ ] Sliding-window limiter scoped to `tenant_id`
- [ ] Per-edition rate-limit policies (Free / Pro / Enterprise)
- [ ] Hard-limit response (429 with `Retry-After`) and soft-limit warning header
- [ ] Operator override via admin API

### 3.6 Migration runner
- [ ] `saas migrate` command (CLI in Phase 9; library-level callable now)
- [ ] Leader election via Postgres advisory lock
- [ ] Per-module migration ordering by dependency graph
- [ ] Concurrent-safe across pool tenants

### Phase 3 Exit Gate
Default deployment is `PoolWithRls`. One tenant promoted to `SiloedDatabase` for an enterprise customer via `ITenantResources` lookup. Cross-tenant leak integration test passes against all four modes.

---

## Phase 4 — Billing, Entitlements & Feature Flags

**Goal:** Monetization is first-class; entitlements (paid gates) and feature flags (rollout) are separate concerns.

### 4.1 Billing core (`SaasBuilder.Modules.Billing`)
- [ ] `IBillingProvider` abstraction with capability matrix per adapter
- [ ] **Stripe adapter** (primary): catalog, subscriptions, metered usage, invoices, customer portal
- [ ] Paddle adapter (Merchant of Record alternative)
- [ ] Lemon Squeezy adapter
- [ ] Chargebee adapter (enterprise)
- [ ] Webhook receiver with HMAC verification + 5-min replay window + idempotency-key dedup
- [ ] Daily reconciliation job (DB ↔ provider drift detection); alerts on drift
- [ ] Stripe Tax / Avalara / TaxJar integration for tax calculation

### 4.2 Plan catalog model
- [ ] `Product` entity (synced from provider)
- [ ] `Price` entity (one-time / recurring / tiered / graduated / volume)
- [ ] `Edition` entity — named bundle of entitlements (ABP pattern); maps to one or more `Price`s
- [ ] `Plan` entity — what's sold to a tenant
- [ ] `Subscription` entity tied to tenant + plan + status (per Stripe states)

### 4.3 Subscription lifecycle
- [ ] Create subscription (Stripe Checkout session / Paddle / Lemon)
- [ ] Upgrade/downgrade with proration
- [ ] Cancel (immediate / at-period-end)
- [ ] Pause / resume
- [ ] Trial flows (with/without payment method)
- [ ] Coupons / promo codes
- [ ] Referral credits

### 4.4 Per-seat & multi-line billing
- [ ] Auto-sync seat count when org members added/removed
- [ ] Multi-line cart: base plan + add-ons + one-time charges
- [ ] Soft limit: warn user (header + email)
- [ ] Hard limit: 402 Payment Required at endpoint

### 4.5 Metered/usage billing
- [ ] `IUsageMeter.RecordAsync(meterId, quantity, idempotencyKey)`
- [ ] Aggregation pipeline (per-tenant per-meter per-period)
- [ ] Push to provider with idempotency key
- [ ] Overage pricing with grace policy

### 4.6 Customer portal
- [ ] Stripe customer-portal session generator endpoint
- [ ] Tenant-facing billing dashboard: invoices list/download, plan changes, payment methods, usage graphs
- [ ] Past-due banner for `payment_failed` state

### 4.7 Dunning
- [ ] Subscribe to `invoice.payment_failed` webhook
- [ ] Branded dunning emails (3-step sequence)
- [ ] Configurable grace period before suspension
- [ ] Tenant suspension on terminal failure (calls Phase 3 lifecycle)

### 4.8 Entitlements (`SaasBuilder.Entitlements`)
- [ ] `IEntitlementService.HasAsync(string key)` returns boolean
- [ ] `IEntitlementService.GetValueAsync<T>(string key)` for numeric/string entitlements
- [ ] Entitlements derived from active edition; cached per tenant; invalidated on `subscription.updated`
- [ ] `[RequiresEntitlement("advanced_reporting")]` ASP.NET Core attribute
- [ ] `[RequiresEntitlement("max_seats", AsLimit = true)]` for numeric limits
- [ ] Tenant-level overrides for sales-driven exceptions

### 4.9 Feature Flags (`SaasBuilder.FeatureFlags`)
- [ ] **OpenFeature**-compatible `IFeatureClient` (CNCF spec)
- [ ] Default DB-backed provider (no external dependency)
- [ ] LaunchDarkly adapter
- [ ] Unleash adapter
- [ ] Flagsmith adapter
- [ ] Flagd adapter
- [ ] Targeting context auto-populated from `ITenantContext`
- [ ] Percentage rollout, segmentation, kill-switch built-in for default provider
- [ ] Admin endpoint to view/override flags per tenant

### Phase 4 Exit Gate
A tenant subscribes via Stripe Checkout, hits `/api/reports/advanced` and gets 403 on Free tier, upgrades to Pro and gets 200, exceeds seat soft-limit and sees a warning, exceeds hard-limit and gets 402. Replayed Stripe webhook is rejected.

---

## Phase 5 — Cross-cutting Primitives

Each sub-module is a separately versioned NuGet package. Each silently degrades when env vars missing.

### 5.1 Notifications (`SaasBuilder.Modules.Notifications`)
- [ ] `INotificationDispatcher` (email, in-app, push, SMS, webhook-out)
- [ ] Email adapters: SendGrid, AWS SES, Postmark, Resend, Mailgun, SMTP fallback
- [ ] Templating: Razor + MJML; per-tenant brand override (logo, colors, footer)
- [ ] Localization (resx + per-tenant override files)
- [ ] In-app notification feed (entity + read state + push via SignalR)
- [ ] Push adapters: APNs, FCM
- [ ] SMS adapters: Twilio, MessageBird
- [ ] User preferences per channel per notification type
- [ ] Digest/batching for daily/weekly rollups
- [ ] Bounce/complaint webhook ingest → suppression list

### 5.2 Files (`SaasBuilder.Modules.Files`)
- [ ] `IBlobStore` abstraction
- [ ] Adapters: FileSystem (dev), Azure Blob, S3, GCS, R2 (Cloudflare)
- [ ] Per-tenant containers/prefixes routed via `ITenantResources`
- [ ] Signed URL upload (presigned PUT, time-boxed)
- [ ] Signed URL download (presigned GET, time-boxed)
- [ ] Browser direct-to-storage upload sample
- [ ] Image processing: resize, thumbnail, WebP via ImageSharp
- [ ] Quota tracking per tenant; alert at 80%, hard-cap at 100%
- [ ] Optional virus-scan adapter (ClamAV)

### 5.3 Background jobs (`SaasBuilder.Modules.Jobs`)
- [ ] `IJobScheduler` abstraction
- [ ] Hangfire adapter (default — has dashboard)
- [ ] Quartz.NET adapter
- [ ] MassTransit scheduled-redelivery adapter (for bus mode)
- [ ] Recurring (cron) + delayed + retry-with-backoff + DLQ
- [ ] **Tenant-aware enqueue** — context auto-restored on dequeue
- [ ] Idempotency key on every job payload
- [ ] Replay-from-DLQ tooling

### 5.4 Audit (`SaasBuilder.Modules.Audit`)
- [ ] Centralized append-only audit table (tenant_id, actor_id, action, resource, before_json, after_json, ip, ua, correlation_id, ts)
- [ ] `IAuditLogger.RecordAsync(...)` API
- [ ] Auto-instrumentation for entity CRUD, login, permission changes, billing events
- [ ] **Hash-chain mode** for tamper-evidence (SOC 2)
- [ ] GDPR export per tenant (CSV/JSON, time-bounded)
- [ ] SIEM forwarding adapters: Splunk HEC, Datadog, syslog
- [ ] Retention policy per edition
- [ ] Search & filter API
- [ ] Distinct from Ledger's domain audit (which is fintech-specific)

### 5.5 Outbound webhooks (`SaasBuilder.Modules.Webhooks`)
- [ ] Conform to **Standard Webhooks** spec (`webhook-id`, `webhook-timestamp`, `webhook-signature`)
- [ ] HMAC-SHA256 per-endpoint with rotatable secret
- [ ] 5-minute timestamp window to prevent replay
- [ ] Subscription manager API (tenant adds/removes endpoints + selects event types)
- [ ] Retries with exponential backoff + jitter (Svix-style schedule)
- [ ] DLQ after N retries; tenant alerted
- [ ] Delivery log per attempt (request, response, status, latency)
- [ ] Replay-from-log button
- [ ] Event registry with JSON schemas
- [ ] Test-send from UI

### 5.6 Search (`SaasBuilder.Modules.Search`)
- [ ] `ISearchClient` abstraction
- [ ] Adapters: Postgres FTS (default), OpenSearch, Meilisearch, Typesense, Algolia
- [ ] Per-tenant index or routing key
- [ ] Indexer pipeline subscribes to domain events
- [ ] Faceting / filters / highlighting
- [ ] **Query-time tenant scope enforcement** (defense in depth)

### 5.7 Realtime (`SaasBuilder.Modules.Realtime`)
- [ ] SignalR with Redis backplane (default)
- [ ] SQL backplane option
- [ ] Tenant-scoped groups auto-join on connect (`tenant:{id}`)
- [ ] Presence (online/offline per org)
- [ ] Broadcast helpers scoped to tenant group
- [ ] Reconnect/replay with last-seen cursor

### Phase 5 Exit Gate
Each cross-cutting module has 3 representative integration tests, a docs page, and demonstrable usage in the starter app. Each module silently degrades when env vars are missing (logs a warning at startup, registers no-op fallback).

---

## Phase 6 — Admin / Control Plane

**Goal:** APIs that empower SaaS operators to support customers without DB access.

### 6.1 `SaasBuilder.Modules.Admin` API surface
- [ ] Tenant directory: list / search / filter / drill-in
- [ ] Tenant inspector: usage, billing, audit summary, support metadata
- [ ] Impersonation launcher (calls Phase 2 endpoint with reason)
- [ ] Per-tenant entitlement override
- [ ] Per-tenant feature-flag override
- [ ] Job dashboard endpoints (list, retry, replay-from-DLQ)
- [ ] Webhook delivery dashboard (list deliveries, replay)
- [ ] Ops health endpoints (DB, queues, providers, SLO status)
- [ ] Support actions: resend invite, force-reset password, refund, credit grant
- [ ] Approval workflow for sensitive actions (configurable per action)

### 6.2 Admin authorization model
- [ ] Distinct `SystemAdmin` role separate from tenant `Owner`
- [ ] All admin endpoints require MFA in current session
- [ ] Admin actions audit-logged with `is_system_admin` flag

### Phase 6 Exit Gate
A SaaS operator resolves three representative tickets end-to-end via admin APIs alone (expired invite resend, stuck subscription manual sync, locked-out user impersonation).

---

## Phase 7 — Frontend SDK & Starter App

### 7.1 Generated TypeScript client
- [ ] Codegen from OpenAPI via Kiota (or NSwag)
- [ ] Published as `@saasbuilder/client` on npm
- [ ] Versioned alongside backend (compat matrix in docs)
- [ ] Auth interceptor (token refresh, MFA challenge handling)

### 7.2 Next.js 16 starter (`saasbuilder/starter-next`)
- [ ] Login page (local + social + magic + SSO)
- [ ] MFA setup wizard
- [ ] Tenant onboarding wizard
- [ ] Member management + invitations UI
- [ ] Billing portal + Stripe Checkout integration
- [ ] In-app notification feed component (SignalR)
- [ ] Webhook subscription manager component
- [ ] File upload demo (presigned)
- [ ] Per-tenant theming (logo, colors via CSS vars)
- [ ] shadcn/ui + Tailwind

### 7.3 Blazor WASM starter (parity for .NET shops)
- [ ] Same surface as Next.js starter
- [ ] MudBlazor or FluentUI design system

### 7.4 Hosted UI (drop-in MVC pages for backend-only consumers)
- [ ] Login
- [ ] MFA setup
- [ ] Accept invitation
- [ ] Billing portal redirect

### 7.5 Admin UI (consumer of Phase 6 APIs)
- [ ] Separate Next.js app under template
- [ ] Tenant directory + inspector
- [ ] Job dashboard
- [ ] Webhook delivery viewer
- [ ] Feature-flag console

### Phase 7 Exit Gate
Starter app deploys to Vercel + Azure App Service in <30 min. Lighthouse 90+ on landing page.

---

## Phase 8 — Compliance & Deployment

### 8.1 GDPR (`SaasBuilder.Modules.Gdpr`)
- [ ] Personal data export (zip) per user / per tenant
- [ ] Right-to-be-forgotten workflow with grace period
- [ ] Consent management (cookie + processing consent records)
- [ ] DPA template generator
- [ ] Sub-processor list management

### 8.2 Encryption & residency
- [ ] DB TDE configuration guidance
- [ ] Phase 3 envelope encryption integrated into compliance docs
- [ ] `SiloedStamp` mode + region pinning per tenant for residency

### 8.3 SOC 2 audit-trail mode
- [ ] Hash-chained audit log enabled by config
- [ ] Retention guarantee with locked storage option
- [ ] Access review report (who-can-see-what per tenant)

### 8.4 Kubernetes deployment
- [ ] Helm chart with HPA + PodDisruptionBudgets
- [ ] Secrets via workload identity / IRSA / managed identities
- [ ] `/health/live`, `/health/ready`, `/health/startup` distinct probes
- [ ] Migration job pre-deployment hook with leader election

### 8.5 IaC samples
- [ ] Bicep for Azure (App Service, ACR, KeyVault, Postgres Flexible, Storage, Service Bus)
- [ ] Terraform for Azure (parity)
- [ ] Terraform for AWS (ECS Fargate, ECR, Secrets Manager, RDS, S3, SQS)
- [ ] Terraform for GCP (Cloud Run, Cloud SQL, GCS, Pub/Sub)

### 8.6 Deployment patterns
- [ ] Blue/green recipe (Azure deployment slots)
- [ ] Canary recipe (Argo Rollouts) — already partial in `src/Chassis.Gateway`
- [ ] Expand-migrate-contract guide for zero-downtime DDL
- [ ] Backup & restore tooling per tenant (point-in-time)

### Phase 8 Exit Gate
A consumer can deploy to AKS or EKS via Helm, satisfy SOC 2 audit-log requirements out of the box, and produce a GDPR data-export within hours of request.

---

## Phase 9 — Developer Experience & Tooling

### 9.1 `SaasBuilder.Cli` (`dotnet tool`)
- [ ] `saas new <name>` — scaffold a new SaaS app (wraps `dotnet new saas-api`)
- [ ] `saas add module <name>` — scaffold a vertical-slice module:
  - `Contracts`, `Domain`, `Application`, `Infrastructure`, `Api` projects
  - DbContext + RLS migration template
  - Endpoint stub + handler + validator + tests
  - `IModuleStartup` implementation
  - Module manifest update
- [ ] `saas add feature <module> <name>` — scaffold a feature in an existing module:
  - Endpoint + DTO + validator + handler
  - Permission seed + OpenAPI fragment
  - Integration test (auth + happy + tenant-leak)
- [ ] `saas migrate` — apply pending migrations per module in dependency order
- [ ] `saas tenant create <slug>` — provision a tenant locally
- [ ] `saas pack` — build all SDK packages with consistent versioning
- [ ] `saas doctor` — diagnostic that checks env vars, DB connectivity, provider configs

### 9.2 Templates
- [ ] `saas-api` template (Phase 1 skeleton → Phase 9 full)
- [ ] `saas-module` template
- [ ] `saas-feature` template
- [ ] `saas-microservice` template (out-of-process module deployment)

### 9.3 Sample apps under `samples/`
- [ ] `samples/B2BSample` — multi-tenant, SAML SSO, billing, RBAC, admin
- [ ] `samples/B2CSample` — single-user-per-tenant, magic link, simple billing
- [ ] `samples/MarketplaceSample` — Phase 10 extension demo

### 9.4 Docs site (`docs.saasbuilder.dev`)
- [ ] Docusaurus or VitePress scaffold
- [ ] "Build your first SaaS in 30 minutes" tutorial
- [ ] "Add a custom module" tutorial
- [ ] "Configure Stripe billing" tutorial
- [ ] "Configure SAML SSO for an enterprise customer" tutorial
- [ ] "Deploy to AKS / EKS / App Service" tutorials
- [ ] Architectural Decision Records (ADRs)
- [ ] API reference auto-generated from OpenAPI per release
- [ ] Versioned (v1.x, v2.x)
- [ ] Searchable

### 9.5 Aspire AppHost integration
- [ ] `samples/SaasBuilder.AspireHost/` orchestrates: Postgres, Redis, RabbitMQ, Mailhog, Azurite, OTel collector
- [ ] One-command local dev experience

### Phase 9 Exit Gate
A new developer follows the docs and builds a "to-do list SaaS" with tenants, billing, SSO, and an admin page in under one workday.

---

## Phase 10 — AI Primitives, Marketplace, GA Launch

### 10.1 AI primitives (`SaasBuilder.Modules.Ai`)
- [ ] `ILlmClient` over `Microsoft.Extensions.AI` (provider-neutral)
- [ ] Provider adapters: OpenAI, Anthropic, Azure OpenAI, AWS Bedrock, Google, Ollama (local)
- [ ] `IEmbeddingClient` abstraction
- [ ] Vector-store abstraction with pgvector default + Qdrant + Pinecone + Azure AI Search adapters
- [ ] **RAG pipeline** with **mandatory tenant scope** on retrieval (security invariant — tested)
- [ ] Prompt safety: input sanitization, output validation, jailbreak detection
- [ ] PII redaction before LLM call
- [ ] **Per-call usage capture** (model, prompt-tokens, completion-tokens, cost) → audit + metering
- [ ] Per-tenant LLM budget with soft + hard caps (integrates with Phase 4 entitlements)
- [ ] Streaming via SSE with cancellation
- [ ] Tool-use / function-calling abstraction
- [ ] Evaluation harness: golden-set tests, regression detection, prompt versioning
- [ ] Semantic + exact prompt-output cache to cut spend
- [ ] **MCP server adapter** — expose tenant data as Model Context Protocol server scoped to caller's tenant

### 10.2 Marketplace (`SaasBuilder.Modules.Marketplace`)
- [ ] Module manifest registry (capabilities, deps, signed packages)
- [ ] **OAuth-app surface** — third parties register apps acting on tenant behalf (Slack-app pattern)
- [ ] Webhook + REST + UI extension points for installed apps
- [ ] Per-tenant install/uninstall flow with admin approval
- [ ] Permission scopes installed apps request; tenant admin grants
- [ ] Optional revenue-share via Phase 4 billing

### 10.3 GA launch
- [ ] Performance benchmark: 1000 RPS sustained on 4-vCPU host (extend `loadtests/nbomber/`)
- [ ] External security audit / pen test
- [ ] Versioning policy + LTS commitment for v1.x documented
- [ ] Migration guides: ABP → SaasBuilder, plain ASP.NET → SaasBuilder
- [ ] README overhaul with case studies
- [ ] Public launch post + Hacker News + dev.to

### Phase 10 Exit Gate
v1.0.0 packages on NuGet.org. Two reference customers in production. Documented LTS policy.

---

## Cross-Phase Tasks (Continuous)

### Architecture & Quality
- [ ] Keep `tests/SaasBuilder.ArchitectureTests/` rules current as new modules land
- [ ] Add **cross-tenant leak test** to every new module's integration suite (assert tenant A cannot see tenant B's data)
- [ ] Add **contract test** for every package consumed externally (smoke test against packed `.nupkg`)
- [ ] Maintain **Module Compatibility Matrix** in docs (which modules co-exist with which)

### Observability
- [ ] Every new handler emits structured logs + traces + RED metrics with `tenant_id` enrichment
- [ ] Per-tenant cardinality bounded (use bucketing for high-cardinality dims)
- [ ] Grafana dashboards-as-code shipped per module under `deploy/grafana/dashboards/`

### Security
- [ ] OWASP ASVS L2 baseline maintained — checklist in PR template
- [ ] Dependabot + GitHub Advanced Security enabled
- [ ] Secrets never in source — `appsettings*.json` audit on every PR

### Documentation
- [ ] Every Phase ships a docs page; merging code without docs is a CI failure
- [ ] CHANGELOG_AI.md updated per significant change (existing convention)
- [ ] ADR for every load-bearing decision

### Sample Modules Migration (one-time, during Phase 1–4)
- [ ] Move `src/Modules/Ledger/` → `samples/Ledger/`
- [ ] Move `src/Modules/Reporting/` → `samples/Reporting/`
- [ ] Move `src/Modules/Registration/` → `samples/Registration/`
- [ ] Promote `src/Modules/Identity/` to `SaasBuilder.Modules.Identity` package (Phase 2)
- [ ] Update `SaasBuilder.sln` accordingly

---

## Estimated Effort

| Phase | Estimate | Parallelizable with |
|---|---|---|
| Phase 1 | 1–2 weeks | — (blocking) |
| Phase 2 | 4 weeks | Phase 3 |
| Phase 3 | 3 weeks | Phase 2, Phase 4 |
| Phase 4 | 4 weeks | Phase 3, Phase 5 |
| Phase 5 | 5 weeks | Phase 4, Phase 6 |
| Phase 6 | 2 weeks | Phase 5, Phase 7 |
| Phase 7 | 4 weeks | Phase 6, Phase 8 |
| Phase 8 | 2 weeks | Phase 7 |
| Phase 9 | 2 weeks | — (depends on stable surface) |
| Phase 10 | 4 weeks | — (final) |
| **Total (sequential)** | **~33 weeks** | |
| **Total (parallel where possible)** | **~22 weeks** | |

A two-engineer team could ship v1.0 GA in ~6 months working in parallel; a solo engineer in ~9 months.
