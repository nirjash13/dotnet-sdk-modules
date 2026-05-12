/**
 * C-28 FIX: Short-lived SignalR hub token endpoint.
 *
 * Client components that need SignalR access fetch this endpoint with their
 * HttpOnly session cookie attached (same-origin). The server reads the cookie,
 * calls the backend to exchange it for a hub-scoped token, and returns that
 * short-lived token to the client. The long-lived access token never reaches
 * the client JS heap or the RSC hydration payload.
 *
 * Backend endpoint required:
 *   POST /api/v1/notifications/hub-token
 *   Authorization: Bearer <access_token>
 *   Response: { hub_token: string, expires_in: number }
 */

import { NextResponse } from "next/server";
import { getTokens } from "@/lib/auth";

const API_BASE =
  process.env.NEXT_API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:5000";

export async function POST(): Promise<NextResponse> {
  const tokens = await getTokens();

  if (!tokens) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${API_BASE}/api/v1/notifications/hub-token`, {
      method: "POST",
      headers: {
        Authorization: `Bearer ${tokens.accessToken}`,
        "Content-Type": "application/json",
      },
    });
  } catch {
    return NextResponse.json(
      { error: "upstream_unavailable" },
      { status: 502 },
    );
  }

  if (!upstream.ok) {
    return NextResponse.json(
      { error: "hub_token_unavailable" },
      { status: upstream.status },
    );
  }

  const data = (await upstream.json()) as {
    hub_token: string;
    expires_in?: number;
  };

  return NextResponse.json({
    hub_token: data.hub_token,
    expires_in: data.expires_in ?? 300,
  });
}
