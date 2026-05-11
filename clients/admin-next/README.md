# SaasBuilder Admin UI

Platform operator console for the SaasBuilder SDK. **Not** intended for end-tenants.

## Intended audience

Platform operators and support engineers who need to:
- Inspect and manage tenant accounts
- Monitor background job queues
- View webhook delivery logs and replay failures
- Toggle feature flags per tenant

## Tech stack

Next.js 16 · TypeScript strict · Tailwind CSS v4 · shadcn/ui (Radix primitives)

## Getting started

```bash
cp .env.local.example .env.local
# fill in NEXT_PUBLIC_API_BASE_URL and OAuth credentials

npm install
npm run dev
# → http://localhost:3001
```

## Authentication

Login via `/auth/login`. Requires `role=admin` in the JWT claims — standard tenant
accounts are rejected at the middleware layer.

## Pages

| Route | Description |
|---|---|
| `/admin/tenants` | Tenant directory with search and status filter |
| `/admin/tenants/[id]` | Tenant inspector: users, plan, usage, suspend/restore |
| `/admin/jobs` | Background-job dashboard (Scheduled / Running / Succeeded / Failed) |
| `/admin/webhooks` | Webhook delivery viewer and replay per tenant |
| `/admin/feature-flags` | Feature flag console with per-tenant toggles |
| `/auth/login` | Admin OAuth login |

## Development notes

- API calls go through `src/lib/api.ts` which injects `Authorization: Bearer <token>` from `sessionStorage`.
- Server Components are the default; mark client components with `"use client"` only when needed.
- Empty states are shown for all pages when the backend endpoint is not yet available.
