<!-- written-by: writer-haiku | model: haiku -->
# @saasbuilder/client

TypeScript client for SaasBuilder SDK REST APIs.

## Install

```bash
npm install @saasbuilder/client
```

## Auth

```ts
import { SaasBuilderClient } from "@saasbuilder/client";

const client = new SaasBuilderClient({
  baseUrl: "https://api.acme.com",
  token: "your-jwt",
  refreshToken: "your-refresh-token",
});
```

Tokens are refreshed automatically on 401. Set `refreshToken` to enable silent renewal.

## Example Usage

```ts
// Fetch current user
const me = await client.request<{ name: string }>("/api/v1/identity/me");

// Post with body
await client.request("/api/v1/identity/invitations", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({ email: "user@acme.com", role: "Member" }),
});

// Per-call cancellation
const controller = new AbortController();
await client.request("/api/v1/identity/members", { signal: controller.signal });
```

## MFA Handling

```ts
import { MfaRequiredError } from "@saasbuilder/client";

try {
  await client.request("/api/v1/protected");
} catch (err) {
  if (err instanceof MfaRequiredError) {
    const code = prompt("Enter your 6-digit code:");
    await client.verifyMfa(code!, err.mfaToken);
    // Retry original request
  }
}
```

## Codegen

Generate TypeScript types from your API's OpenAPI document:

```bash
npm run generate
```

This invokes `scripts/generate.ts` which calls NSwag against `http://localhost:5000/openapi/v1.json`
and emits `src/generated/api.ts`.

## Build

```bash
npm run build   # compiles to dist/
npm run lint    # type-check only
```
