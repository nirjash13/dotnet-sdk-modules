import { NextResponse } from "next/server";

/**
 * C-21 SECURITY FIX: This endpoint has been removed.
 *
 * The previous implementation accepted any { access_token, refresh_token } JSON body
 * and wrote it into HttpOnly cookies with no signature verification, no audience check,
 * and no CSRF/state binding — allowing session-fixation by any same-origin script.
 *
 * CORRECT REPLACEMENT (out of scope for this patch — requires backend coordination):
 *   1. The login page redirects to the backend /connect/authorize with PKCE challenge.
 *   2. The backend redirects to /api/auth/code-callback?code=...&state=...
 *   3. This route handler validates state against a server-session value, then POSTs
 *      code + code_verifier to the backend /connect/token server-to-server.
 *   4. Only after successful server-side token exchange are HttpOnly cookies set.
 *
 * Until that flow is implemented, the social-login callback is handled by the
 * backend HostedUI, which sets cookies directly and redirects to /dashboard.
 * The password-grant login path now uses /api/auth/login (server route handler)
 * instead of posting tokens to this endpoint.
 */
export function POST(): NextResponse {
  return new NextResponse(
    JSON.stringify({
      error: "gone",
      detail:
        "Direct token injection is not permitted. Use the Authorization Code + PKCE flow.",
    }),
    {
      status: 410,
      headers: { "Content-Type": "application/json" },
    },
  );
}
