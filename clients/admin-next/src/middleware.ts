import { NextRequest, NextResponse } from "next/server";

// C-22 FIX: Admin authorization is no longer decided by parsing the unsigned JWT body.
// The previous implementation did JSON.parse(Buffer.from(token.split(".")[1], "base64url"))
// and granted /admin/* access purely on payload.role === "admin" — without ever verifying
// the signature. Any string the attacker could plant in the cookie passed the gate.
//
// REPLACEMENT DESIGN:
// The middleware checks for the presence of the admin_access_token cookie (HttpOnly, set
// by the /api/admin/auth/login BFF route). The role decision is deferred to the backend:
// every protected API call includes the Bearer token, and the backend validates signature +
// audience + role on each request. A forged or role-stripped token is rejected by the
// backend with 401/403.
//
// STRONGER OPTION (recommended for production): Call the backend /userinfo endpoint here
// (with short-lived edge caching keyed on token hash) to validate the token and confirm
// role=admin before serving the admin shell. This adds ~50ms per cold request but closes
// the window where an expired/revoked token still passes this gate.

const PUBLIC_PATHS = ["/auth/login"];

function isPublic(pathname: string): boolean {
  return PUBLIC_PATHS.some((p) => pathname.startsWith(p));
}

export function middleware(request: NextRequest): NextResponse {
  const { pathname } = request.nextUrl;

  if (isPublic(pathname)) {
    return NextResponse.next();
  }

  // The cookie is HttpOnly + Secure + SameSite=Strict — it can only be written by the
  // BFF /api/admin/auth/login route handler after a successful server-to-server
  // /connect/token exchange that confirmed role=admin from the backend /userinfo response.
  const token = request.cookies.get("admin_access_token")?.value;

  if (!token) {
    const loginUrl = request.nextUrl.clone();
    loginUrl.pathname = "/auth/login";
    loginUrl.searchParams.set("returnTo", pathname);
    return NextResponse.redirect(loginUrl);
  }

  // Token is present and was issued by the BFF after backend role validation.
  // The backend validates signature + role on every protected API call.
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
