import { NextRequest, NextResponse } from "next/server";

const PUBLIC_PATHS = ["/auth/login"];

function isPublic(pathname: string): boolean {
  return PUBLIC_PATHS.some((p) => pathname.startsWith(p));
}

function hasAdminRole(token: string): boolean {
  try {
    const payload = JSON.parse(
      Buffer.from(token.split(".")[1]!, "base64url").toString("utf-8"),
    ) as Record<string, unknown>;

    const role =
      payload["role"] ??
      payload[
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
      ];

    return role === "admin" || (Array.isArray(role) && role.includes("admin"));
  } catch {
    return false;
  }
}

export function middleware(request: NextRequest): NextResponse {
  const { pathname } = request.nextUrl;

  if (isPublic(pathname)) {
    return NextResponse.next();
  }

  const token = request.cookies.get("admin_access_token")?.value;

  if (!token || !hasAdminRole(token)) {
    const loginUrl = request.nextUrl.clone();
    loginUrl.pathname = "/auth/login";
    loginUrl.searchParams.set("returnTo", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
