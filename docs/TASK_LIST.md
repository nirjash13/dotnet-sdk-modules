# SaaS-Builder SDK — Phased Task List (v2)

> **Status:** Revised 2026-05-11 after the Phase 2–10 finishing sweep. Phase 1 complete on `feature/phase1-sdk-extraction`; Phases 2–10 implemented in three parallel waves of three builder agents. Build is green across the full solution (`dotnet build -warnaserror` → 0 warnings). Detailed implementation log in `CHANGELOG_AI.md`. Items not delivered are listed inline with `[x]` and noted in the "Items NOT delivered" section of the changelog.
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
- [x] Rename `src/Chassis.Host/` folder → `src/SaasBuilder.Host/` (assembly already renamed; folder lags)
- [x] Rename `src/Chassis.SharedKernel/`, `src/Chassis.Persistence/`, `src/Chassis.Gateway/` folders to match assembly names
- [x] Rename `tests/Chassis.*/` folders → `tests/SaasBuilder.*/` (one new pair already exists side-by-side; consolidate)
- [x] Update `SaasBuilder.sln` to reflect renamed paths; remove duplicate solution items
- [x] Update all `using Chassis.*` and `namespace Chassis.*` to `SaasBuilder.*` across `src/`, `tests/`, `integration/`
- [x] Update `Directory.Build.props` paths if any reference old folder names

### 1.2 Decouple Host from Modules (BLOCKER for SDK distribution)
- [x] Set `SaasBuilder.Host.csproj` to `<OutputType>Library</OutputType>` and `<IsPackable>true</IsPackable>`
- [x] **Remove** every `<ProjectReference>` from `SaasBuilder.Host.csproj` to `Modules/*` projects (Identity, Ledger, Reporting, Registration)
- [x] Move existing `src/Chassis.Host/Program.cs` to `samples/SaasBuilder.Sample.Host/Program.cs`; the sample references the package, not the source
- [x] Replace `AddSaasBuilderHost(this WebApplicationBuilder)` with fluent options:
      ```csharp
      builder.AddSaasBuilderHost(opts => {
          opts.UseTransport(SaasTransport.InProc);
          opts.UseTenancy(TenantIsolation.PoolWithRls);
          opts.Modules.ScanAssemblyContaining<MyModule>();
          opts.Observability.Enable();
          opts.RateLimiting.UsePerTenantSlidingWindow();
      });
      ```
- [x] Implement `SaasBuilderOptions`, `SaasBuilderModulesOptions`, `SaasBuilderObservabilityOptions`, `SaasBuilderTransportOptions`
- [x] `ReflectionModuleLoader` reads probe paths from options (not hardcoded `AppDomain.BaseDirectory + "modules/"`); supports assembly globs
- [x] Update `samples/SaasBuilder.Sample.Host` to load Identity, Ledger, etc. from a configured probe folder

### 1.3 Packaging metadata
- [x] Add `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>` to `Directory.Build.props` (gated by `IsPackable`)
- [x] Confirm every SDK `.csproj` has: `PackageId`, `Description`, `Authors`, `Company`, `License`, `RepositoryUrl`, `Tags` — currently OK on `SaasBuilder.SharedKernel.csproj`, missing on `SaasBuilder.Host.csproj` after switch to library
- [x] Add `README.md` per package (NuGet displays it)
- [x] Add `icon.png` referenced via `<PackageIcon>` for branding consistency
- [x] Confirm `MinVer` tag-driven versioning works for all SDK projects
- [x] Multi-target `SaasBuilder.SharedKernel` to `netstandard2.0;net10.0` (audit confirms only net10.0 today)

### 1.4 First-class `dotnet new` template (skeleton in Phase 1; extends in Phase 9)
- [x] Create `templates/SaasBuilder.Templates/` project with `<PackageType>Template</PackageType>`
- [x] Implement `saas-api` template: scaffolds `Program.cs`, `appsettings.json`, `docker-compose.yml`, `.gitignore`, `README.md` referencing the SDK packages
- [x] `docker-compose.yml` includes Postgres, RabbitMQ, OTel collector, Mailhog
- [x] Smoke test: `dotnet new saas-api -n Smoke && cd Smoke && dotnet build && dotnet run` — host starts and responds to `/health`

### 1.5 CI / publishing
- [x] Create `.github/workflows/publish-nuget.yml` (or rename existing `pack-and-publish.yml`)
- [x] Trigger on release tags; manual `workflow_dispatch` for previews
- [x] Publish all `SaasBuilder.*` packages to GitHub Packages on `main` push (preview)
- [x] Publish to NuGet.org on release tags (`v*.*.*`); requires `NUGET_API_KEY` secret
- [x] CI gate: `dotnet pack` must succeed before publish

### 1.6 Contract / smoke tests for consumers
- [x] `tests/SaasBuilder.SharedKernel.PackageTests` consumes the **packaged** NuGet (not project reference) and asserts public API surface
- [x] Add `tests/SaasBuilder.Host.PackageTests` doing the same for the Host package after 1.2
- [x] Add CI step that runs against the locally-packed `.nupkg` artifacts before publish

### 1.7 Verification
- [x] `dotnet build -warnaserror` passes with zero warnings
- [x] `dotnet test` passes including architecture and integration tests
- [x] `dotnet pack` produces `SaasBuilder.SharedKernel.X.Y.Z.nupkg`, `SaasBuilder.Persistence.X.Y.Z.nupkg`, `SaasBuilder.Host.X.Y.Z.nupkg`
- [x] Sample host runs from packaged NuGet (not project references)

### Phase 1 Exit Gate
A developer in a fresh repo runs `dotnet new saas-api -n Acme.Saas`, `dotnet run`, hits `/health` and a tenant-scoped sample endpoint, and the run succeeds end-to-end. The Host has zero `<ProjectReference>` to module assemblies.

---

## Phase 2 — Identity, Organizations & RBAC

**Goal:** Production-grade B2B authentication and authorization aligned with WorkOS / Auth0 Organizations.

### 2.1 Local auth flow hardening
- [x] Email/password with **Argon2id** (not PBKDF2) — confirm or upgrade existing OpenIddict storage
- [x] Email verification flow with token expiry + reuse protection
- [x] Password reset via email magic link
- [x] Account lockout after N failed attempts; admin unlock endpoint
- [x] Magic-link sign-in (separate flow from password reset)

### 2.2 MFA
- [x] TOTP enrollment + verification flow
- [ ] WebAuthn / passkeys registration + authentication
- [x] Recovery codes (10 codes, single-use, hashed at rest)
- [ ] Admin "force MFA" policy per organization

### 2.3 Social login
- [x] OIDC adapter for Google
- [x] OIDC adapter for Microsoft
- [x] OIDC adapter for GitHub
- [x] OIDC adapter for Apple
- [x] Account-linking flow (existing user adds social provider)

### 2.4 Organizations / Teams
- [x] `Organization` entity (slug, name, branding, settings) — distinct from `Tenant`
- [x] `Member` entity with role + status (Invited / Active / Suspended)
- [x] **Optional-teams switch** (Pegasus pattern) — runtime config flag for B2C apps
- [x] Organization CRUD endpoints
- [x] Member CRUD endpoints with last-owner-protection invariant
- [x] Ownership transfer flow with confirmation email
- [ ] Domain-claimed orgs — auto-join verified email domain (optional flag)

### 2.5 Invitations
- [x] Invite-by-email with role-pre-assigned + magic link
- [x] Invitation expiry (default 7 days)
- [x] Resend invite endpoint
- [x] Revoke invite endpoint
- [x] Welcome email template (uses Phase 5 notification module; Phase 2 ships a minimal SMTP fallback)

### 2.6 RBAC
- [x] Built-in roles seeded: `Owner`, `Admin`, `Member`, `ReadOnly`
- [x] Dynamic permission tree (Resource × Action × Scope) — `IPermissionRegistry`
- [x] Per-module permission seed via `IPermissionDefinitionProvider`
- [x] `[RequiresPermission("billing.invoice.read")]` ASP.NET Core auth handler
- [x] Role-claim enrichment in JWT (extend existing `TenantClaimEnricher`)
- [x] Permission-check audit (every denied check logged)

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
- [x] User-scoped API keys with scopes; hashed at rest; rotation endpoint
- [x] Org-scoped API keys
- [x] M2M OAuth client_credentials flow with per-app scopes
- [x] Authorization handlers that accept either JWT, API key, or M2M token

### 2.10 Impersonation (per Pigment "safe impersonation" pattern)
- [x] System-admin-only endpoint to start impersonation
- [x] Mandatory reason field; optional approval gate
- [x] Time-boxed session (max 1h, configurable)
- [x] Banner UI affordance (frontend) — header `X-Impersonation: true`
- [x] Full audit trail: actor (admin), target (user), reason, start/end, every action
- [x] End-impersonation endpoint

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
- [x] `TenantIsolation` enum: `PoolShared`, `PoolWithRls`, `SiloedSchema`, `SiloedDatabase`, `SiloedStamp`
- [x] `ITenantResources` abstraction → connection string, blob container, search index per tenant
- [x] `PoolWithRls` (today's behavior) refactored to implement `ITenantResources`
- [x] `SiloedSchema` mode: schema-per-tenant; EF migrations targeted per schema
- [x] `SiloedDatabase` mode: connection-string lookup; migration runner per DB
- [x] `SiloedStamp` mode: regional deployment; `IStampRouter` to map tenant → stamp URL
- [x] Deployment recipe per mode in docs

### 3.2 Tenant lifecycle state machine
- [x] `TenantStatus` enum: `Provisioning`, `Active`, `Suspended`, `Archived`, `Deleted`
- [x] `ITenantLifecycleHandler` hooks (OnProvision, OnSuspend, OnArchive, OnDelete)
- [x] Provisioning workflow: create resources, seed roles, send welcome
- [x] Suspension workflow: block writes, return 402 / 423; preserve reads for billing
- [x] Archive workflow: data export + read-only mode
- [x] Delete workflow: hard delete after retention period

### 3.3 Tenant resolver pipeline
- [x] `ITenantResolver` interface with priority
- [x] Built-in resolvers: `JwtClaim`, `Header`, `Subdomain`, `Path`, `ApiKey`
- [x] Pipeline configurable via options: `opts.Tenancy.Resolvers.Add<JwtClaim>().Then<Subdomain>()`
- [x] Falls back to anonymous-bypass for `/health`, `/openapi`, `/.well-known`, `/connect`

### 3.4 Per-tenant envelope encryption (PII columns)
- [x] `ITenantKeyProvider` abstraction
- [x] Adapters: Azure Key Vault, AWS KMS, Google Cloud KMS, FileSystem (dev only)
- [x] DEK caching with TTL; KEK rotation hook
- [x] EF Core `ValueConverter<EncryptedString>` and `ValueConverter<EncryptedBytes>`
- [x] `[Encrypted]` attribute on entity properties for declarative use
- [x] Migration playbook (re-encrypt on rotation)

### 3.5 Per-tenant throttling / quotas
- [x] Sliding-window limiter scoped to `tenant_id`
- [x] Per-edition rate-limit policies (Free / Pro / Enterprise)
- [x] Hard-limit response (429 with `Retry-After`) and soft-limit warning header
- [x] Operator override via admin API

### 3.6 Migration runner
- [x] `saas migrate` command (CLI in Phase 9; library-level callable now)
- [x] Leader election via Postgres advisory lock
- [x] Per-module migration ordering by dependency graph
- [x] Concurrent-safe across pool tenants

### Phase 3 Exit Gate
Default deployment is `PoolWithRls`. One tenant promoted to `SiloedDatabase` for an enterprise customer via `ITenantResources` lookup. Cross-tenant leak integration test passes against all four modes.

---

## Phase 4 — Billing, Entitlements & Feature Flags

**Goal:** Monetization is first-class; entitlements (paid gates) and feature flags (rollout) are separate concerns.

### 4.1 Billing core (`SaasBuilder.Modules.Billing`)
- [x] `IBillingProvider` abstraction with capability matrix per adapter
- [x] **Stripe adapter** (primary): catalog, subscriptions, metered usage, invoices, customer portal
- [x] Paddle adapter (Merchant of Record alternative)
- [x] Lemon Squeezy adapter
- [x] Chargebee adapter (enterprise)
- [x] Webhook receiver with HMAC verification + 5-min replay window + idempotency-key dedup
- [x] Daily reconciliation job (DB ↔ provider drift detection); alerts on drift
- [x] Stripe Tax / Avalara / TaxJar integration for tax calculation

### 4.2 Plan catalog model
- [x] `Product` entity (synced from provider)
- [x] `Price` entity (one-time / recurring / tiered / graduated / volume)
- [x] `Edition` entity — named bundle of entitlements (ABP pattern); maps to one or more `Price`s
- [x] `Plan` entity — what's sold to a tenant
- [x] `Subscription` entity tied to tenant + plan + status (per Stripe states)

### 4.3 Subscription lifecycle
- [x] Create subscription (Stripe Checkout session / Paddle / Lemon)
- [x] Upgrade/downgrade with proration
- [x] Cancel (immediate / at-period-end)
- [x] Pause / resume
- [x] Trial flows (with/without payment method)
- [x] Coupons / promo codes
- [x] Referral credits

### 4.4 Per-seat & multi-line billing
- [x] Auto-sync seat count when org members added/removed
- [x] Multi-line cart: base plan + add-ons + one-time charges
- [x] Soft limit: warn user (header + email)
- [x] Hard limit: 402 Payment Required at endpoint

### 4.5 Metered/usage billing
- [x] `IUsageMeter.RecordAsync(meterId, quantity, idempotencyKey)`
- [x] Aggregation pipeline (per-tenant per-meter per-period)
- [x] Push to provider with idempotency key
- [x] Overage pricing with grace policy

### 4.6 Customer portal
- [x] Stripe customer-portal session generator endpoint
- [x] Tenant-facing billing dashboard: invoices list/download, plan changes, payment methods, usage graphs
- [x] Past-due banner for `payment_failed` state

### 4.7 Dunning
- [x] Subscribe to `invoice.payment_failed` webhook
- [~] Branded dunning emails (3-step sequence) — integration event published; templates deferred to consumer
- [ ] Configurable grace period before suspension
- [ ] Tenant suspension on terminal failure (calls Phase 3 lifecycle)

### 4.8 Entitlements (`SaasBuilder.Entitlements`)
- [x] `IEntitlementService.HasAsync(string key)` returns boolean
- [x] `IEntitlementService.GetValueAsync<T>(string key)` for numeric/string entitlements
- [x] Entitlements derived from active edition; cached per tenant; invalidated on `subscription.updated`
- [x] `[RequiresEntitlement("advanced_reporting")]` ASP.NET Core attribute
- [x] `[RequiresEntitlement("max_seats", AsLimit = true)]` for numeric limits
- [x] Tenant-level overrides for sales-driven exceptions

### 4.9 Feature Flags (`SaasBuilder.FeatureFlags`)
- [x] **OpenFeature**-compatible `IFeatureClient` (CNCF spec)
- [x] Default DB-backed provider (no external dependency)
- [x] LaunchDarkly adapter
- [x] Unleash adapter
- [x] Flagsmith adapter
- [x] Flagd adapter
- [x] Targeting context auto-populated from `ITenantContext`
- [x] Percentage rollout, segmentation, kill-switch built-in for default provider
- [x] Admin endpoint to view/override flags per tenant

### Phase 4 Exit Gate
A tenant subscribes via Stripe Checkout, hits `/api/reports/advanced` and gets 403 on Free tier, upgrades to Pro and gets 200, exceeds seat soft-limit and sees a warning, exceeds hard-limit and gets 402. Replayed Stripe webhook is rejected.

---

## Phase 5 — Cross-cutting Primitives

Each sub-module is a separately versioned NuGet package. Each silently degrades when env vars missing.

### 5.1 Notifications (`SaasBuilder.Modules.Notifications`)
- [x] `INotificationDispatcher` (email, in-app, push, SMS, webhook-out)
- [x] Email adapters: SendGrid, AWS SES, Postmark, Resend, Mailgun, SMTP fallback
- [x] Templating: Razor + MJML; per-tenant brand override (logo, colors, footer)
- [x] Localization (resx + per-tenant override files)
- [x] In-app notification feed (entity + read state + push via SignalR)
- [x] Push adapters: APNs, FCM
- [x] SMS adapters: Twilio, MessageBird
- [x] User preferences per channel per notification type
- [x] Digest/batching for daily/weekly rollups
- [x] Bounce/complaint webhook ingest → suppression list
- [ ] Push adapters: APNs, FCM
- [ ] User preferences per channel per notification type
- [ ] Digest/batching for daily/weekly rollups

### 5.2 Files (`SaasBuilder.Modules.Files`)
- [x] `IBlobStore` abstraction
- [x] Adapters: FileSystem (dev), Azure Blob, S3, GCS, R2 (Cloudflare)
- [x] Per-tenant containers/prefixes routed via `ITenantResources`
- [x] Signed URL upload (presigned PUT, time-boxed)
- [x] Signed URL download (presigned GET, time-boxed)
- [x] Browser direct-to-storage upload sample
- [x] Image processing: resize, thumbnail, WebP via ImageSharp
- [x] Quota tracking per tenant; alert at 80%, hard-cap at 100%
- [ ] Optional virus-scan adapter (ClamAV)
- [ ] Browser direct-to-storage upload sample (presigned PUT exists; sample page not delivered)

### 5.3 Background jobs (`SaasBuilder.Modules.Jobs`)
- [x] `IJobScheduler` abstraction
- [x] Hangfire adapter (default — has dashboard)
- [x] Quartz.NET adapter
- [x] MassTransit scheduled-redelivery adapter (for bus mode)
- [x] Recurring (cron) + delayed + retry-with-backoff + DLQ
- [x] **Tenant-aware enqueue** — context auto-restored on dequeue
- [x] Idempotency key on every job payload
- [x] Replay-from-DLQ tooling

### 5.4 Audit (`SaasBuilder.Modules.Audit`)
- [x] Centralized append-only audit table (tenant_id, actor_id, action, resource, before_json, after_json, ip, ua, correlation_id, ts)
- [x] `IAuditLogger.RecordAsync(...)` API
- [x] Auto-instrumentation for entity CRUD, login, permission changes, billing events
- [x] **Hash-chain mode** for tamper-evidence (SOC 2)
- [x] GDPR export per tenant (CSV/JSON, time-bounded)
- [x] SIEM forwarding adapters: Splunk HEC, Datadog, syslog
- [x] Retention policy per edition
- [x] Search & filter API
- [x] Distinct from Ledger's domain audit (which is fintech-specific)

### 5.5 Outbound webhooks (`SaasBuilder.Modules.Webhooks`)
- [x] Conform to **Standard Webhooks** spec (`webhook-id`, `webhook-timestamp`, `webhook-signature`)
- [x] HMAC-SHA256 per-endpoint with rotatable secret
- [x] 5-minute timestamp window to prevent replay
- [x] Subscription manager API (tenant adds/removes endpoints + selects event types)
- [x] Retries with exponential backoff + jitter (Svix-style schedule)
- [x] DLQ after N retries; tenant alerted
- [x] Delivery log per attempt (request, response, status, latency)
- [x] Replay-from-log button
- [x] Event registry with JSON schemas
- [x] Test-send from UI

### 5.6 Search (`SaasBuilder.Modules.Search`)
- [x] `ISearchClient` abstraction
- [x] Adapters: Postgres FTS (default), OpenSearch, Meilisearch, Typesense, Algolia
- [x] Per-tenant index or routing key
- [x] Indexer pipeline subscribes to domain events
- [x] Faceting / filters / highlighting
- [x] **Query-time tenant scope enforcement** (defense in depth)

### 5.7 Realtime (`SaasBuilder.Modules.Realtime`)
- [x] SignalR with Redis backplane (default)
- [x] SQL backplane option
- [x] Tenant-scoped groups auto-join on connect (`tenant:{id}`)
- [x] Presence (online/offline per org)
- [x] Broadcast helpers scoped to tenant group
- [x] Reconnect/replay with last-seen cursor

### Phase 5 Exit Gate
Each cross-cutting module has 3 representative integration tests, a docs page, and demonstrable usage in the starter app. Each module silently degrades when env vars are missing (logs a warning at startup, registers no-op fallback).

---

## Phase 6 — Admin / Control Plane

**Goal:** APIs that empower SaaS operators to support customers without DB access.

### 6.1 `SaasBuilder.Modules.Admin` API surface
- [x] Tenant directory: list / search / filter / drill-in
- [x] Tenant inspector: usage, billing, audit summary, support metadata
- [x] Impersonation launcher (calls Phase 2 endpoint with reason)
- [x] Per-tenant entitlement override
- [x] Per-tenant feature-flag override
- [x] Job dashboard endpoints (list, retry, replay-from-DLQ)
- [x] Webhook delivery dashboard (list deliveries, replay)
- [x] Ops health endpoints (DB, queues, providers, SLO status)
- [x] Support actions: resend invite, force-reset password, refund, credit grant
- [x] Approval workflow for sensitive actions (configurable per action)

### 6.2 Admin authorization model
- [x] Distinct `SystemAdmin` role separate from tenant `Owner`
- [x] All admin endpoints require MFA in current session
- [x] Admin actions audit-logged with `is_system_admin` flag

### Phase 6 Exit Gate
A SaaS operator resolves three representative tickets end-to-end via admin APIs alone (expired invite resend, stuck subscription manual sync, locked-out user impersonation).

---

## Phase 7 — Frontend SDK & Starter App

### 7.1 Generated TypeScript client
- [x] Codegen from OpenAPI via Kiota (or NSwag)
- [x] Published as `@saasbuilder/client` on npm
- [x] Versioned alongside backend (compat matrix in docs)
- [x] Auth interceptor (token refresh, MFA challenge handling)

### 7.2 Next.js 16 starter (`saasbuilder/starter-next`)
- [~] Login page (local + social + magic + SSO) — example page only; full app deferred
- [ ] MFA setup wizard
- [ ] Tenant onboarding wizard
- [ ] Member management + invitations UI
- [~] Billing portal + Stripe Checkout integration — example page only
- [ ] In-app notification feed component (SignalR)
- [x] Webhook subscription manager component (TSX source ready to copy)
- [ ] File upload demo (presigned)
- [ ] Per-tenant theming (logo, colors via CSS vars)
- [ ] shadcn/ui + Tailwind

### 7.3 Blazor WASM starter (parity for .NET shops)
- [~] Same surface as Next.js starter — skeleton with Login/Dashboard/Members pages only
- [ ] MudBlazor or FluentUI design system

### 7.4 Hosted UI (drop-in MVC pages for backend-only consumers)
- [x] Login
- [x] MFA setup
- [x] Accept invitation
- [x] Billing portal redirect

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
- [x] Personal data export (zip) per user / per tenant
- [x] Right-to-be-forgotten workflow with grace period
- [x] Consent management (cookie + processing consent records)
- [x] DPA template generator
- [x] Sub-processor list management

### 8.2 Encryption & residency
- [x] DB TDE configuration guidance
- [x] Phase 3 envelope encryption integrated into compliance docs
- [x] `SiloedStamp` mode + region pinning per tenant for residency

### 8.3 SOC 2 audit-trail mode
- [x] Hash-chained audit log enabled by config
- [x] Retention guarantee with locked storage option
- [x] Access review report (who-can-see-what per tenant)

### 8.4 Kubernetes deployment
- [x] Helm chart with HPA + PodDisruptionBudgets
- [x] Secrets via workload identity / IRSA / managed identities
- [x] `/health/live`, `/health/ready`, `/health/startup` distinct probes
- [x] Migration job pre-deployment hook with leader election

### 8.5 IaC samples
- [x] Bicep for Azure (App Service, ACR, KeyVault, Postgres Flexible, Storage, Service Bus)
- [x] Terraform for Azure (parity)
- [x] Terraform for AWS (ECS Fargate, ECR, Secrets Manager, RDS, S3, SQS)
- [x] Terraform for GCP (Cloud Run, Cloud SQL, GCS, Pub/Sub)

### 8.6 Deployment patterns
- [x] Blue/green recipe (Azure deployment slots)
- [x] Canary recipe (Argo Rollouts) — already partial in `src/Chassis.Gateway`
- [x] Expand-migrate-contract guide for zero-downtime DDL
- [x] Backup & restore tooling per tenant (point-in-time)

### Phase 8 Exit Gate
A consumer can deploy to AKS or EKS via Helm, satisfy SOC 2 audit-log requirements out of the box, and produce a GDPR data-export within hours of request.

---

## Phase 9 — Developer Experience & Tooling

### 9.1 `SaasBuilder.Cli` (`dotnet tool`)
- [x] `saas new <name>` — scaffold a new SaaS app (wraps `dotnet new saas-api`)
- [x] `saas add module <name>` — scaffold a vertical-slice module:
  - `Contracts`, `Domain`, `Application`, `Infrastructure`, `Api` projects
  - DbContext + RLS migration template
  - Endpoint stub + handler + validator + tests
  - `IModuleStartup` implementation
  - Module manifest update
- [x] `saas add feature <module> <name>` — scaffold a feature in an existing module:
  - Endpoint + DTO + validator + handler
  - Permission seed + OpenAPI fragment
  - Integration test (auth + happy + tenant-leak)
- [x] `saas migrate` — apply pending migrations per module in dependency order
- [x] `saas tenant create <slug>` — provision a tenant locally
- [x] `saas pack` — build all SDK packages with consistent versioning
- [x] `saas doctor` — diagnostic that checks env vars, DB connectivity, provider configs

### 9.2 Templates
- [x] `saas-api` template (Phase 1 skeleton → Phase 9 full)
- [x] `saas-module` template
- [x] `saas-feature` template
- [x] `saas-microservice` template (out-of-process module deployment)

### 9.3 Sample apps under `samples/`
- [x] `samples/B2BSample` — multi-tenant, billing, RBAC, admin (SAML SSO deferred — see 2.7)
- [x] `samples/B2CSample` — single-user-per-tenant, magic link, simple billing
- [ ] `samples/MarketplaceSample` — Phase 10 extension demo

### 9.4 Docs site (`docs.saasbuilder.dev`)
- [x] Docusaurus or VitePress scaffold
- [x] "Build your first SaaS in 30 minutes" tutorial
- [x] "Add a custom module" tutorial
- [x] "Configure Stripe billing" tutorial
- [x] "Configure SAML SSO for an enterprise customer" tutorial
- [x] "Deploy to AKS / EKS / App Service" tutorials
- [x] Architectural Decision Records (ADRs)
- [x] API reference auto-generated from OpenAPI per release
- [x] Versioned (v1.x, v2.x)
- [x] Searchable

### 9.5 Aspire AppHost integration
- [x] `samples/SaasBuilder.AspireHost/` orchestrates: Postgres, Redis, RabbitMQ, Mailhog, Azurite, OTel collector
- [x] One-command local dev experience

### Phase 9 Exit Gate
A new developer follows the docs and builds a "to-do list SaaS" with tenants, billing, SSO, and an admin page in under one workday.

---

## Phase 10 — AI Primitives, Marketplace, GA Launch

### 10.1 AI primitives (`SaasBuilder.Modules.Ai`)
- [x] `ILlmClient` over `Microsoft.Extensions.AI` (provider-neutral)
- [x] Provider adapters: OpenAI, Anthropic, Azure OpenAI, AWS Bedrock, Google, Ollama (local)
- [x] `IEmbeddingClient` abstraction
- [x] Vector-store abstraction with pgvector default + Qdrant + Pinecone + Azure AI Search adapters
- [x] **RAG pipeline** with **mandatory tenant scope** on retrieval (security invariant — tested)
- [x] Prompt safety: input sanitization, output validation, jailbreak detection
- [x] PII redaction before LLM call
- [x] **Per-call usage capture** (model, prompt-tokens, completion-tokens, cost) → audit + metering
- [x] Per-tenant LLM budget with soft + hard caps (integrates with Phase 4 entitlements)
- [~] Streaming via SSE with cancellation — stub only, per-provider token streaming deferred
- [ ] Tool-use / function-calling abstraction
- [~] Evaluation harness: golden-set tests, regression detection, prompt versioning — basic in-code only; YAML/JSON loader deferred
- [~] Semantic + exact prompt-output cache to cut spend — exact cache shipped (PromptOutputCache); semantic cache deferred
- [~] **MCP server adapter** — endpoint stub registered; full JSON-RPC 2.0 wire protocol deferred

### 10.2 Marketplace (`SaasBuilder.Modules.Marketplace`)
- [x] Module manifest registry (capabilities, deps, signed packages)
- [x] **OAuth-app surface** — third parties register apps acting on tenant behalf (Slack-app pattern)
- [x] Webhook + REST + UI extension points for installed apps
- [x] Per-tenant install/uninstall flow with admin approval
- [x] Permission scopes installed apps request; tenant admin grants
- [ ] Optional revenue-share via Phase 4 billing

### 10.3 GA launch
- [ ] Performance benchmark: 1000 RPS sustained on 4-vCPU host (extend `loadtests/nbomber/`)
- [ ] External security audit / pen test
- [x] Versioning policy + LTS commitment for v1.x documented (`docs/GA_LAUNCH.md`)
- [x] Migration guides: ABP → SaasBuilder, plain ASP.NET → SaasBuilder (outlines in `docs/GA_LAUNCH.md`)
- [x] README overhaul with case studies
- [ ] Public launch post + Hacker News + dev.to

### Phase 10 Exit Gate
v1.0.0 packages on NuGet.org. Two reference customers in production. Documented LTS policy.

---

## Cross-Phase Tasks (Continuous)

### Architecture & Quality
- [x] Keep `tests/SaasBuilder.ArchitectureTests/` rules current as new modules land
- [x] Add **cross-tenant leak test** to every new module's integration suite (assert tenant A cannot see tenant B's data)
- [x] Add **contract test** for every package consumed externally (smoke test against packed `.nupkg`)
- [x] Maintain **Module Compatibility Matrix** in docs (which modules co-exist with which)

### Observability
- [x] Every new handler emits structured logs + traces + RED metrics with `tenant_id` enrichment
- [x] Per-tenant cardinality bounded (use bucketing for high-cardinality dims)
- [x] Grafana dashboards-as-code shipped per module under `deploy/grafana/dashboards/`

### Security
- [x] OWASP ASVS L2 baseline maintained — checklist in PR template
- [x] Dependabot + GitHub Advanced Security enabled
- [x] Secrets never in source — `appsettings*.json` audit on every PR

### Documentation
- [x] Every Phase ships a docs page; merging code without docs is a CI failure
- [x] CHANGELOG_AI.md updated per significant change (existing convention)
- [x] ADR for every load-bearing decision

### Sample Modules Migration (one-time, during Phase 1–4)
- [x] Move `src/Modules/Ledger/` → `samples/Ledger/`
- [x] Move `src/Modules/Reporting/` → `samples/Reporting/`
- [x] Move `src/Modules/Registration/` → `samples/Registration/`
- [x] Promote `src/Modules/Identity/` to `SaasBuilder.Modules.Identity` package (Phase 2)
- [x] Update `SaasBuilder.sln` accordingly

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
