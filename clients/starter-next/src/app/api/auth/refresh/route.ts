import { NextRequest, NextResponse } from "next/server";
import { getTokens, refreshTokens, setTokenCookies } from "@/lib/auth";

export async function POST(_request: NextRequest): Promise<NextResponse> {
  const current = await getTokens();

  if (!current?.refreshToken) {
    return NextResponse.json({ error: "No refresh token" }, { status: 401 });
  }

  const refreshed = await refreshTokens(current.refreshToken);

  if (!refreshed) {
    return NextResponse.json(
      { error: "Token refresh failed" },
      { status: 401 },
    );
  }

  await setTokenCookies(refreshed.accessToken, refreshed.refreshToken ?? undefined);

  return NextResponse.json({ access_token: refreshed.accessToken });
}
