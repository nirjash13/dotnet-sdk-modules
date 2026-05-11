import { NextRequest, NextResponse } from "next/server";
import { setTokenCookies } from "@/lib/auth";

interface CallbackBody {
  access_token: string;
  refresh_token?: string;
}

export async function POST(request: NextRequest): Promise<NextResponse> {
  const body = (await request.json()) as CallbackBody;

  if (!body.access_token) {
    return NextResponse.json(
      { error: "access_token is required" },
      { status: 400 },
    );
  }

  await setTokenCookies(body.access_token, body.refresh_token);

  return NextResponse.json({ ok: true });
}
