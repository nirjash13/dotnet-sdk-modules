import { NextRequest, NextResponse } from "next/server";

const PROTECTED_PATTERN =
  /^\/(dashboard|members|billing|files|notifications|webhooks|settings|onboarding)(\/.*)?$/;

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
