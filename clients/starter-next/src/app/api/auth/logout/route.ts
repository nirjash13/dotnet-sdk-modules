import { NextRequest, NextResponse } from "next/server";
import { clearTokenCookies, getTokens } from "@/lib/auth";

export async function POST(_request: NextRequest): Promise<NextResponse> {
  const tokens = await getTokens();

  if (tokens?.accessToken) {
    const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";
    // Best-effort server-side logout — don't fail if backend is unreachable
    await fetch(`${apiBase}/connect/logout`, {
      method: "POST",
      headers: { Authorization: `Bearer ${tokens.accessToken}` },
    }).catch(() => undefined);
  }

  await clearTokenCookies();

  return NextResponse.json({ ok: true });
}
