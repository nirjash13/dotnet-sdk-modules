/**
 * C-29 FIX: Server-side BFF login route.
 *
 * Replaces the previous pattern where the client POSTed credentials directly to
 * /connect/token (ROPC from the browser) and then forwarded the raw token to
 * /api/auth/callback (open injection endpoint, C-21).
 *
 * This route performs the token exchange server-to-server so the raw access token
 * never appears in the browser's network tab, RSC payload, or JS heap.
 * HttpOnly cookies are set here, not on the client.
 *
 * NOTE: For a fully compliant public-client SPA, replace this entire flow with
 * Authorization Code + PKCE (redirect to /connect/authorize). ROPC is used here
 * only as a transitional measure while the PKCE redirect flow is being wired.
 * The client_id is passed server-side via NEXT_API_CLIENT_ID (not NEXT_PUBLIC_*).
 */

import { NextRequest, NextResponse } from "next/server";
import { setTokenCookies } from "@/lib/auth";

interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  error?: string;
  mfa_required?: boolean;
  mfa_token?: string;
}

const API_BASE =
  process.env.NEXT_API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:5000";

// Server-only client ID — never exposed to the browser
const CLIENT_ID = process.env.NEXT_API_CLIENT_ID ?? "starter-ui";

export async function POST(request: NextRequest): Promise<NextResponse> {
  const body = (await request.json().catch(() => null)) as {
    email?: string;
    password?: string;
  } | null;

  if (!body?.email || !body.password) {
    return NextResponse.json(
      { error: "email and password are required" },
      { status: 400 },
    );
  }

  const form = new URLSearchParams({
    grant_type: "password",
    username: body.email,
    password: body.password,
    scope: "openid offline_access",
    client_id: CLIENT_ID,
  });

  let upstream: Response;
  try {
    upstream = await fetch(`${API_BASE}/connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: form.toString(),
    });
  } catch {
    return NextResponse.json(
      { error: "upstream_unavailable" },
      { status: 502 },
    );
  }

  const data = (await upstream.json()) as TokenResponse;

  if (!upstream.ok) {
    // Propagate MFA challenge to the client (the mfa_token is a short-lived opaque
    // token — not a JWT — so passing it to the browser is acceptable).
    if (data.mfa_required && data.mfa_token) {
      return NextResponse.json(
        { mfa_required: true, mfa_token: data.mfa_token },
        { status: 200 },
      );
    }

    return NextResponse.json(
      { error: data.error ?? "invalid_credentials" },
      { status: upstream.status },
    );
  }

  // Set HttpOnly cookies server-side — access token never reaches the browser JS heap.
  await setTokenCookies(data.access_token, data.refresh_token);

  return NextResponse.json({ ok: true });
}
