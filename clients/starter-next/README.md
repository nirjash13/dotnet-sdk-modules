<!-- written-by: writer-haiku | model: haiku -->
# SaasBuilder — Next.js 16 Starter

A production-ready Next.js 16 / TypeScript / Tailwind / shadcn-ui scaffold wired to the SaasBuilder backend. Clone and `pnpm install` to start.

---

## Setup

```bash
git clone <repo>
cd clients/starter-next
cp .env.local.example .env.local   # fill in your values
pnpm install
pnpm dev
```

### Environment Variables

| Variable | Required | Description |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | yes | SaasBuilder API base URL (no trailing slash) |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | for billing | Stripe publishable key |
| `COOKIE_SECRET` | yes (prod) | 32-byte secret for cookie signing |

---

## Project Structure

```
src/
├── app/
│   ├── (auth)/
│   │   ├── layout.tsx                  # Centered auth shell
│   │   ├── login/page.tsx              # Local + magic-link + social + MFA challenge
│   │   ├── mfa-setup/page.tsx          # TOTP QR wizard + recovery codes
│   │   └── accept-invitation/page.tsx  # Token-based invite acceptance
│   ├── (onboarding)/
│   │   ├── layout.tsx                  # Onboarding shell
│   │   └── onboarding/page.tsx         # Create org → invite → pick plan
│   ├── (app)/
│   │   ├── layout.tsx                  # Auth guard + AppShell (sidebar + topbar)
│   │   ├── dashboard/page.tsx
│   │   ├── members/page.tsx
│   │   ├── billing/page.tsx
│   │   ├── files/page.tsx
│   │   ├── notifications/page.tsx
│   │   ├── webhooks/page.tsx
│   │   └── settings/
│   │       ├── profile/page.tsx
│   │       └── branding/page.tsx
│   ├── api/
│   │   └── auth/
│   │       ├── callback/route.ts       # POST — store access + refresh tokens in httpOnly cookies
│   │       ├── logout/route.ts         # POST — clear cookies
│   │       └── refresh/route.ts        # POST — exchange refresh token
│   ├── layout.tsx                      # Root layout — font, ThemeProvider, Toaster, branding vars
│   ├── globals.css                     # Tailwind + CSS custom properties + dark mode
│   ├── error.tsx                       # Global error boundary
│   └── not-found.tsx
├── components/
│   ├── ui/                             # Radix-based primitives (no CLI required)
│   │   ├── button.tsx, input.tsx, label.tsx, card.tsx
│   │   ├── dialog.tsx, dropdown-menu.tsx, toast.tsx, form.tsx
│   │   ├── table.tsx, badge.tsx, skeleton.tsx, tabs.tsx
│   ├── app-shell.tsx                   # Sidebar + topbar + impersonation banner
│   ├── theme-provider.tsx              # Light/dark/system toggle
│   ├── mfa-challenge.tsx               # Inline TOTP/recovery-code prompt
│   ├── invite-modal.tsx                # Invite member dialog
│   ├── notification-feed.tsx           # SignalR-powered live feed
│   ├── file-uploader.tsx               # Presigned PUT upload
│   ├── webhook-manager.tsx             # Subscription + delivery log
│   ├── members-table.tsx               # Paginated team table
│   └── plan-selector.tsx              # Checkout session redirect
├── lib/
│   ├── api.ts          # Fetch wrapper — bearer from cookie, auto-refresh on 401, MFA passthrough
│   ├── auth.ts         # Cookie helpers, requireTokens(), refreshTokens()
│   ├── signalr.ts      # Reconnecting SignalR singleton for /hubs/notifications
│   ├── stripe.ts       # loadStripe singleton + redirectToCheckout
│   └── cn.ts           # clsx + tailwind-merge
└── middleware.ts        # Redirect unauthenticated requests on (app) routes to /login
```

---

## Auth Flow

Tokens are stored in `httpOnly` cookies via the BFF pattern — never in `localStorage`.

1. User submits credentials → client POSTs to `/connect/token`.
2. Response tokens forwarded to `POST /api/auth/callback` → stored as `sb_token` + `sb_refresh` cookies.
3. Server components read `sb_token` via `cookies()`.
4. Client components call `apiFetch()` which auto-refreshes on 401 via `POST /api/auth/refresh`.
5. Social login: redirect to `/connect/authorize?provider=Google|Microsoft|GitHub|Apple`; callback handled by `api/auth/callback/route.ts`.

---

## Backend Endpoints Consumed

| Feature | Endpoint |
|---|---|
| Password grant | `POST /connect/token` |
| Token refresh | `POST /connect/token` (grant_type=refresh_token) |
| Social OIDC | `GET /connect/authorize?provider=...` |
| Magic link | `POST /api/v1/identity/magic-link` |
| MFA enroll | `GET /api/v1/identity/mfa/setup/totp` |
| MFA verify | `POST /api/v1/identity/mfa/verify` |
| Recovery codes | `GET /api/v1/identity/mfa/recovery-codes` |
| Current user | `GET /api/v1/identity/me` |
| Tenant branding | `GET /api/v1/branding` |
| Organizations | `GET/POST /api/v1/organizations` |
| Members | `GET /api/v1/organizations/{id}/members` |
| Invitations | `POST /api/v1/organizations/{id}/invitations` |
| Checkout | `POST /api/v1/billing/checkout-session` |
| Portal | `POST /api/v1/billing/portal-session` |
| Files list | `GET /api/v1/files` |
| Presigned upload | `POST /api/v1/files/upload-url` |
| Notifications | `GET /api/v1/notifications` + SignalR `/hubs/notifications` |
| Webhooks | `GET/POST/DELETE /api/v1/webhooks` |
| Branding (per-tenant) | `GET /api/v1/branding` |

---

## Per-Tenant Branding

The root layout fetches `/api/v1/branding` (ISR, 5-min revalidate) and injects `--brand-primary` as a CSS variable on `<body>`. All Tailwind theme tokens reference CSS custom properties — swapping the brand color is a single variable override.

---

## Deployment

**Vercel:**
```bash
vercel env add NEXT_PUBLIC_API_URL
vercel env add NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY
vercel --prod
```

**Azure App Service (Node 20):**
```bash
az webapp create --name my-saas --runtime "NODE:20-lts" --plan my-plan -g my-rg
az webapp config appsettings set --name my-saas -g my-rg \
  --settings NEXT_PUBLIC_API_URL=https://api.acme.com
az webapp deploy --name my-saas -g my-rg --src-path .next/standalone
```

Enable `output: "standalone"` in `next.config.mjs` for standalone Azure deployments.

---

## Example Pages

The `example-pages/` folder contains the original copy-paste page components for reference. The `src/` directory is the primary scaffold.
