/**
 * C-23 FIX: Admin API client.
 *
 * Previous: getToken() read from sessionStorage; setToken() wrote to both sessionStorage
 * and a non-HttpOnly document.cookie. Both storage locations are readable by any XSS on
 * the admin origin, and an admin-token theft compromises every tenant.
 *
 * Now: The admin_access_token is stored in an HttpOnly + Secure + SameSite=Strict cookie
 * set exclusively by the /api/admin/auth/login BFF route. Client-side code never touches
 * the token value. API requests from client components go to BFF proxy routes that attach
 * the token server-side, OR go directly to the backend and rely on the backend validating
 * the token from the cookie (requires backend CORS + cookie forwarding config).
 *
 * For admin API calls from client components, the simplest safe pattern is:
 *   - Create thin Next.js route handlers under /api/admin/** that read the HttpOnly cookie
 *     server-side and proxy requests to the backend.
 *   - Client components fetch /api/admin/** instead of the backend directly.
 *
 * The helpers below are kept for server-component / route-handler use only.
 * They read the cookie via the Next.js `cookies()` API (server runtime only).
 */

const API_BASE =
  process.env.NEXT_API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "http://localhost:5000";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

type FetchOptions = Omit<RequestInit, "headers"> & {
  headers?: Record<string, string>;
  token?: string;
};

async function request<T>(path: string, options: FetchOptions = {}): Promise<T> {
  const { token, ...fetchOptions } = options;
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers ?? {}),
  };

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...fetchOptions,
    headers,
  });

  if (response.status === 401) {
    // In a client component context, redirect to login
    if (typeof window !== "undefined") {
      window.location.href = "/auth/login";
    }
    throw new ApiError(401, "Unauthorized");
  }

  if (!response.ok) {
    const body = await response.text().catch(() => response.statusText);
    throw new ApiError(response.status, body);
  }

  if (response.status === 204) {
    return undefined as unknown as T;
  }

  return response.json() as Promise<T>;
}

/**
 * Server-side API helpers for use in Route Handlers and Server Components.
 * Pass the token explicitly (read from cookies() in the calling server context).
 *
 * Example (in a route handler):
 *   import { cookies } from "next/headers";
 *   const token = (await cookies()).get("admin_access_token")?.value;
 *   const tenants = await api.get<Tenant[]>("/api/v1/admin/tenants", token);
 */
export const api = {
  get: <T>(path: string, token?: string) =>
    request<T>(path, { method: "GET", token }),

  post: <T>(path: string, body?: unknown, token?: string) =>
    request<T>(path, {
      method: "POST",
      token,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),

  put: <T>(path: string, body?: unknown, token?: string) =>
    request<T>(path, {
      method: "PUT",
      token,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),

  patch: <T>(path: string, body?: unknown, token?: string) =>
    request<T>(path, {
      method: "PATCH",
      token,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),

  delete: <T>(path: string, token?: string) =>
    request<T>(path, { method: "DELETE", token }),
};
