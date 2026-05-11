<!-- written-by: writer-haiku | model: haiku -->
# SaasBuilder — Next.js Starter Manifest

This manifest describes how to build a full-featured SaaS frontend against the SaasBuilder backend.
The `example-pages/` folder contains copy-paste page components.

---

## Quick Start

```bash
npx create-next-app@latest my-saas --typescript --tailwind --eslint --app
cd my-saas
npm install @saasbuilder/client
```

Copy the files from `example-pages/` into `app/` or `pages/` as your starting point.

---

## Required Backend Endpoints

| Page | Method + Path | Auth |
|---|---|---|
| Login | `POST /connect/token` | None |
| Logout | `POST /connect/logout` | Bearer |
| Magic link | `POST /api/v1/identity/magic-link` | None |
| MFA enroll | `GET /api/v1/identity/mfa/totp/enroll` | Bearer |
| MFA verify | `POST /api/v1/identity/mfa/verify` | Bearer |
| Accept invitation | `POST /api/v1/identity/invitations/accept` | None (token in body) |
| Current user | `GET /api/v1/identity/me` | Bearer |
| Tenant branding | `GET /api/v1/identity/me/tenant` | Bearer |
| Members list | `GET /api/v1/identity/members` | Bearer |
| Invite member | `POST /api/v1/identity/invitations` | Bearer + `members.invite` permission |
| Billing portal | `POST /api/v1/billing/customer-portal/session` | Bearer |
| Subscription | `GET /api/v1/billing/subscription` | Bearer |
| Webhook endpoints | `GET /api/v1/webhooks/endpoints` | Bearer |
| Webhook deliveries | `GET /api/v1/webhooks/deliveries` | Bearer |

---

## Suggested Page Structure

```
app/
  (auth)/
    login/page.tsx          # Local + social + magic link + SSO
    signup/page.tsx         # Self-serve signup (B2C)
    accept-invitation/page.tsx
    mfa-setup/page.tsx
  (app)/
    layout.tsx              # Auth guard + tenant branding shell
    dashboard/page.tsx      # Landing after login
    billing/page.tsx        # Subscription + portal redirect
    settings/page.tsx       # Profile, password, API keys
    members/page.tsx        # Member list + invite form
    admin/page.tsx          # Operator admin panel (SystemAdmin only)
    webhooks/page.tsx       # Webhook subscription manager
```

---

## Initialise the Client

```ts
// lib/client.ts
import { SaasBuilderClient } from "@saasbuilder/client";

export function createClient(token?: string, refreshToken?: string) {
  return new SaasBuilderClient({
    baseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000",
    token,
    refreshToken,
  });
}
```

Store tokens in `httpOnly` cookies via a Next.js Route Handler for BFF pattern:

```ts
// app/api/auth/callback/route.ts
import { cookies } from "next/headers";
import { NextResponse } from "next/server";

export async function POST(request: Request) {
  const { access_token, refresh_token } = await request.json();
  (await cookies()).set("sb_token", access_token, { httpOnly: true, secure: true, sameSite: "lax" });
  if (refresh_token) {
    (await cookies()).set("sb_refresh", refresh_token, { httpOnly: true, secure: true, sameSite: "lax" });
  }
  return NextResponse.json({ ok: true });
}
```

---

## Component Examples

### Using the Client in a Server Component

```tsx
import { cookies } from "next/headers";
import { createClient } from "@/lib/client";

export default async function DashboardPage() {
  const token = (await cookies()).get("sb_token")?.value;
  const client = createClient(token);
  const me = await client.request<{ name: string; tenantName: string }>("/api/v1/identity/me");
  return <h1>Welcome, {me.name}</h1>;
}
```

### Handling MFA

```tsx
import { MfaRequiredError } from "@saasbuilder/client";

try {
  await client.request("/api/v1/some-protected-route");
} catch (err) {
  if (err instanceof MfaRequiredError) {
    router.push(`/mfa-verify?token=${err.mfaToken}`);
  }
}
```

---

## Theming — Per-Tenant Branding

Fetch tenant settings on layout mount and apply as CSS variables:

```ts
interface TenantBranding {
  primaryColor: string;
  logoUrl: string;
  name: string;
}

const tenant = await client.request<TenantBranding>("/api/v1/identity/me/tenant");

// In layout.tsx <html> or <body>:
// style={{ "--color-primary": tenant.primaryColor } as React.CSSProperties }
```

Use Tailwind CSS arbitrary values or CSS `var()` in your components:

```css
/* globals.css */
:root {
  --color-primary: #6366f1; /* fallback */
}
```

---

## Lighthouse + Vercel Deployment

**Target scores:** Performance 90+, Accessibility 90+, Best Practices 95+.

Checklist:
- Enable Next.js Image Optimization for tenant logos.
- Use `loading="lazy"` on below-fold images.
- Ship only what's needed: use dynamic imports for the WebhookManager.
- Set `Cache-Control: private, max-age=0` on authenticated API routes.
- Enable HSTS + security headers in `next.config.ts` (headers array).

**Deploy to Vercel:**

```bash
npm i -g vercel
vercel env add NEXT_PUBLIC_API_URL
vercel --prod
```

Set `NEXT_PUBLIC_API_URL` to your SaasBuilder API URL in Vercel's project settings.

**Deploy to Azure App Service (Node):**

```bash
az webapp create --name my-saas --runtime "NODE:20-lts" --plan my-plan -g my-rg
az webapp config appsettings set --name my-saas -g my-rg \
  --settings NEXT_PUBLIC_API_URL=https://api.acme.com
az webapp deploy --name my-saas -g my-rg --src-path .next/standalone
```
