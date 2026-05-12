import { NextRequest, NextResponse } from "next/server";

const PROTECTED_PATTERN =
  /^\/(dashboard|members|billing|files|notifications|webhooks|settings|onboarding)(\/.*)?$/;

// C-27 NOTE: This middleware checks for the *presence* of the sb_token cookie as a
// first-line gate. It does NOT verify the JWT signature (Edge Runtime cannot fetch
// JWKS without latency on every request).
//
// SECURITY MODEL: The sb_token cookie is HttpOnly + Secure + SameSite=Lax.
// It can only be written by the BFF server route (/api/auth/login), which performs
// server-to-server token exchange with the backend. A forged or expired cookie will
// be rejected by the backend API (which validates the signature on every protected
// request), resulting in a 401 → redirect to /login from the client component.
//
// TO STRENGTHEN: Call the backend /userinfo endpoint here (with edge caching) to
// validate the token before serving the page shell. This adds ~50ms per request
// but eliminates the window where an expired token still passes the middleware gate.
export function middleware(request: NextRequest): NextResponse {
  const token = request.cookies.get("sb_token")?.value;
  const { pathname } = request.nextUrl;

  if (PROTECTED_PATTERN.test(pathname) && !token) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("next", pathname);
    return NextResponse.redirect(loginUrl);
  }

  // Redirect authenticated users away from the login page
  if (token && pathname === "/login") {
    return NextResponse.redirect(new URL("/dashboard", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico|api/).*)"],
};
