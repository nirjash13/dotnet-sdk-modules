/**
 * Auth helpers for server components and route handlers.
 * Tokens are stored as httpOnly cookies — never in localStorage.
 */

import { cookies } from "next/headers";
import { redirect } from "next/navigation";

export interface TokenSet {
  accessToken: string;
  refreshToken: string | null;
}

export async function getTokens(): Promise<TokenSet | null> {
  const jar = await cookies();
  const accessToken = jar.get("sb_token")?.value;
  if (!accessToken) return null;
  return {
    accessToken,
    refreshToken: jar.get("sb_refresh")?.value ?? null,
  };
}

export async function requireTokens(): Promise<TokenSet> {
  const tokens = await getTokens();
  if (!tokens) redirect("/login");
  return tokens;
}

export async function requireAccessToken(): Promise<string> {
  const tokens = await requireTokens();
  return tokens.accessToken;
}

const COOKIE_OPTIONS = {
  httpOnly: true,
  secure: process.env.NODE_ENV === "production",
  sameSite: "lax" as const,
  path: "/",
};

export async function setTokenCookies(
  accessToken: string,
  refreshToken?: string,
): Promise<void> {
  const jar = await cookies();
  jar.set("sb_token", accessToken, {
    ...COOKIE_OPTIONS,
    maxAge: 60 * 60, // 1 hour
  });
  if (refreshToken) {
    jar.set("sb_refresh", refreshToken, {
      ...COOKIE_OPTIONS,
      maxAge: 60 * 60 * 24 * 30, // 30 days
    });
  }
}

export async function clearTokenCookies(): Promise<void> {
  const jar = await cookies();
  jar.delete("sb_token");
  jar.delete("sb_refresh");
}

/** Exchange a refresh token for a new access token at the backend. */
export async function refreshTokens(
  refreshToken: string,
): Promise<TokenSet | null> {
  const apiBase =
    process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

  const form = new URLSearchParams({
    grant_type: "refresh_token",
    refresh_token: refreshToken,
  });

  try {
    const res = await fetch(`${apiBase}/connect/token`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: form.toString(),
    });

    if (!res.ok) return null;

    const data = (await res.json()) as {
      access_token: string;
      refresh_token?: string;
    };

    return {
      accessToken: data.access_token,
      refreshToken: data.refresh_token ?? refreshToken,
    };
  } catch {
    return null;
  }
}
