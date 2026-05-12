/**
 * C-22 + C-23 FIX: Admin BFF login route.
 *
 * Previous: client POSTed credentials to /connect/token, decoded the JWT body client-side
 * to check role=admin (no signature verification), then wrote the token to sessionStorage
 * AND a non-HttpOnly document.cookie. Both storage locations are readable by XSS.
 *
 * Now: credentials are sent here (server route). This handler:
 *   1. Exchanges credentials server-to-server at /connect/token (token never hits the browser).
 *   2. Calls /userinfo server-to-server to verify role=admin from a signed server response.
 *   3. Only if role=admin: sets an HttpOnly + Secure + SameSite=Strict cookie.
 *   4. Returns { ok: true } — no token in the response body.
 *
 * The client login page only posts { email, password } and reads { ok, error }.
 * The admin_access_token cookie is readable only by the Next.js server runtime.
 */

import { NextRequest, NextResponse } from "next/server";
import { cookies } from "next/headers";

const API_BASE =
  process.env.NEXT_API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "http://localhost:5000";

// Server-only env var — never exposed to browser
const CLIENT_ID = process.env.NEXT_API_ADMIN_CLIENT_ID ?? "admin-ui";

interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  token_type?: string;
  error?: string;
}

interface UserInfoResponse {
  role?: string | string[];
  [key: string]: unknown;
}

function hasAdminRole(userInfo: UserInfoResponse): boolean {
  const role =
    userInfo["role"] ??
    userInfo["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];
  return role === "admin" || (Array.isArray(role) && role.includes("admin"));
}

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

  // Step 1: Exchange credentials for tokens (server-to-server)
  const form = new URLSearchParams({
    grant_type: "password",
    username: body.email,
    password: body.password,
    scope: "openid offline_access",
    client_id: CLIENT_ID,
  });

  let tokenRes: Response;
  try {
    tokenRes = await fetch(`${API_BASE}/connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: form.toString(),
    });
  } catch {
    return NextResponse.json({ error: "upstream_unavailable" }, { status: 502 });
  }

  if (!tokenRes.ok) {
    return NextResponse.json(
      { error: "invalid_credentials" },
      { status: 401 },
    );
  }

  const tokenData = (await tokenRes.json()) as TokenResponse;

  // Step 2: Validate role=admin via signed /userinfo (server-to-server)
  let userInfoRes: Response;
  try {
    userInfoRes = await fetch(`${API_BASE}/connect/userinfo`, {
      headers: { Authorization: `Bearer ${tokenData.access_token}` },
    });
  } catch {
    return NextResponse.json({ error: "upstream_unavailable" }, { status: 502 });
  }

  if (!userInfoRes.ok) {
    return NextResponse.json({ error: "userinfo_unavailable" }, { status: 502 });
  }

  const userInfo = (await userInfoRes.json()) as UserInfoResponse;

  if (!hasAdminRole(userInfo)) {
    return NextResponse.json(
      { error: "insufficient_permissions" },
      { status: 403 },
    );
  }

  // Step 3: Set HttpOnly + Secure + SameSite=Strict cookie (never readable by JS)
  const jar = await cookies();
  const isProduction = process.env.NODE_ENV === "production";

  jar.set("admin_access_token", tokenData.access_token, {
    httpOnly: true,
    secure: isProduction,
    sameSite: "strict",
    path: "/",
    maxAge: 60 * 60, // 1 hour
  });

  if (tokenData.refresh_token) {
    jar.set("admin_refresh_token", tokenData.refresh_token, {
      httpOnly: true,
      secure: isProduction,
      sameSite: "strict",
      path: "/api/admin/auth",
      maxAge: 60 * 60 * 24 * 30, // 30 days, refresh-only path
    });
  }

  return NextResponse.json({ ok: true });
}
