/**
 * Thin fetch wrapper used by both Server Components (token from cookie) and
 * Client Components (token from in-memory store after BFF callback).
 *
 * Auto-refresh: on 401 response, attempts one token refresh then retries.
 * MFA passthrough: 403 responses with mfa_required=true propagate as
 * MfaRequiredError so callers can redirect to /mfa-verify.
 */

const API_BASE =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly detail: string,
    public readonly code?: string,
  ) {
    super(detail);
    this.name = "ApiError";
  }
}

export class MfaRequiredError extends Error {
  constructor(public readonly mfaToken: string) {
    super("MFA required");
    this.name = "MfaRequiredError";
  }
}

export class UnauthorizedError extends ApiError {
  constructor() {
    super(401, "Unauthorized");
    this.name = "UnauthorizedError";
  }
}

interface RequestOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
  token?: string;
}

async function refreshAccessToken(): Promise<string | null> {
  try {
    const res = await fetch("/api/auth/refresh", { method: "POST" });
    if (!res.ok) return null;
    const data = (await res.json()) as { access_token?: string };
    return data.access_token ?? null;
  } catch {
    return null;
  }
}

async function executeRequest<T>(
  path: string,
  options: RequestOptions,
  token?: string,
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  if (!res.ok) {
    let detail = res.statusText;
    let code: string | undefined;

    try {
      const err = (await res.json()) as {
        detail?: string;
        title?: string;
        code?: string;
        mfa_required?: boolean;
        mfa_token?: string;
      };

      if (res.status === 403 && err.mfa_required && err.mfa_token) {
        throw new MfaRequiredError(err.mfa_token);
      }

      detail = err.detail ?? err.title ?? detail;
      code = err.code;
    } catch (inner) {
      if (inner instanceof MfaRequiredError) throw inner;
    }

    if (res.status === 401) throw new UnauthorizedError();
    throw new ApiError(res.status, detail, code);
  }

  if (res.status === 204) return undefined as unknown as T;
  return res.json() as Promise<T>;
}

/**
 * Server-side fetch — pass `token` explicitly (read from cookie via `cookies()`).
 * Client-side fetch — omit token; the BFF cookie is sent automatically via
 * the `/api/auth/refresh` round-trip.
 */
export async function apiFetch<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  try {
    return await executeRequest<T>(path, options, options.token);
  } catch (err) {
    if (err instanceof UnauthorizedError && !options.token) {
      // Attempt silent refresh (BFF cookie is present)
      const newToken = await refreshAccessToken();
      if (newToken) {
        return executeRequest<T>(path, options, newToken);
      }
    }
    throw err;
  }
}

/** Convenience helpers */
export const apiGet = <T>(path: string, token?: string) =>
  apiFetch<T>(path, { method: "GET", token });

export const apiPost = <T>(path: string, body?: unknown, token?: string) =>
  apiFetch<T>(path, { method: "POST", body, token });

export const apiPut = <T>(path: string, body?: unknown, token?: string) =>
  apiFetch<T>(path, { method: "PUT", body, token });

export const apiPatch = <T>(path: string, body?: unknown, token?: string) =>
  apiFetch<T>(path, { method: "PATCH", body, token });

export const apiDelete = <T>(path: string, token?: string) =>
  apiFetch<T>(path, { method: "DELETE", token });
