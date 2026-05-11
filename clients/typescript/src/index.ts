export * from "./errors.js";

import {
  ForbiddenError,
  MfaRequiredError,
  NotFoundError,
  SaasBuilderError,
  UnauthorizedError,
  ValidationError,
} from "./errors.js";

export interface SaasBuilderClientOptions {
  /** Base URL of the SaasBuilder API (e.g. https://api.acme.com). */
  baseUrl: string;
  /** Initial access token (JWT). */
  token?: string;
  /** Refresh token used to obtain a new access token on 401. */
  refreshToken?: string;
  /** Override the token endpoint path. Defaults to /connect/token. */
  tokenEndpoint?: string;
}

interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  expires_in?: number;
}

interface MfaCheckResponse {
  requires_mfa: true;
  mfa_token: string;
}

function isMfaRequired(body: unknown): body is MfaCheckResponse {
  return (
    typeof body === "object" &&
    body !== null &&
    (body as Record<string, unknown>)["requires_mfa"] === true &&
    typeof (body as Record<string, unknown>)["mfa_token"] === "string"
  );
}

/**
 * Thin wrapper around fetch for SaasBuilder REST APIs.
 *
 * Features:
 * - Automatic Bearer token injection.
 * - Automatic token refresh on 401 when a refresh token is available.
 * - MFA-challenge detection: throws MfaRequiredError so callers can prompt the user.
 * - Per-call AbortSignal support.
 */
export class SaasBuilderClient {
  private readonly baseUrl: string;
  private readonly tokenEndpoint: string;
  private _token: string | undefined;
  private _refreshToken: string | undefined;

  constructor(options: SaasBuilderClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.tokenEndpoint = options.tokenEndpoint ?? "/connect/token";
    this._token = options.token;
    this._refreshToken = options.refreshToken;
  }

  get token(): string | undefined {
    return this._token;
  }

  /**
   * Perform a fetch request with automatic token injection and 401 refresh.
   */
  async request<T = unknown>(
    path: string,
    init: RequestInit & { signal?: AbortSignal } = {},
  ): Promise<T> {
    const response = await this.fetchWithAuth(path, init);

    if (!response.ok) {
      await this.throwForStatus(response);
    }

    const contentType = response.headers.get("content-type") ?? "";
    if (!contentType.includes("application/json")) {
      return undefined as T;
    }

    const body: unknown = await response.json();

    // MFA challenge: 200 OK but requires MFA step-up
    if (isMfaRequired(body)) {
      throw new MfaRequiredError(body.mfa_token);
    }

    return body as T;
  }

  /**
   * Complete an MFA challenge and update the stored tokens.
   * @param code  The TOTP/OTP code from the user.
   * @param mfaToken  The mfa_token from the MfaRequiredError.
   */
  async verifyMfa(code: string, mfaToken: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/api/v1/identity/mfa/verify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ code, mfa_token: mfaToken }),
    });

    if (!response.ok) {
      await this.throwForStatus(response);
    }

    const body = (await response.json()) as TokenResponse;
    this._token = body.access_token;
    if (body.refresh_token) {
      this._refreshToken = body.refresh_token;
    }
  }

  private async fetchWithAuth(
    path: string,
    init: RequestInit & { signal?: AbortSignal },
  ): Promise<Response> {
    const headers = new Headers(init.headers as HeadersInit | undefined);
    if (this._token) {
      headers.set("Authorization", `Bearer ${this._token}`);
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers,
    });

    // On 401, attempt a token refresh once
    if (response.status === 401 && this._refreshToken) {
      const refreshed = await this.refreshAccessToken();
      if (refreshed) {
        headers.set("Authorization", `Bearer ${this._token}`);
        return fetch(`${this.baseUrl}${path}`, { ...init, headers });
      }
    }

    return response;
  }

  private async refreshAccessToken(): Promise<boolean> {
    try {
      const body = new URLSearchParams({
        grant_type: "refresh_token",
        refresh_token: this._refreshToken ?? "",
      });

      const response = await fetch(`${this.baseUrl}${this.tokenEndpoint}`, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: body.toString(),
      });

      if (!response.ok) {
        this._token = undefined;
        this._refreshToken = undefined;
        return false;
      }

      const data = (await response.json()) as TokenResponse;
      this._token = data.access_token;
      if (data.refresh_token) {
        this._refreshToken = data.refresh_token;
      }
      return true;
    } catch {
      return false;
    }
  }

  private async throwForStatus(response: Response): Promise<never> {
    let body: unknown;
    try {
      body = await response.json();
    } catch {
      body = await response.text().catch(() => undefined);
    }

    switch (response.status) {
      case 401:
        throw new UnauthorizedError(body);
      case 403:
        throw new ForbiddenError(body);
      case 404:
        throw new NotFoundError();
      case 400:
      case 422: {
        const errors =
          typeof body === "object" &&
          body !== null &&
          "errors" in body
            ? (body as { errors: Record<string, string[]> }).errors
            : {};
        throw new ValidationError(errors, body);
      }
      default:
        throw new SaasBuilderError(
          `Request failed with status ${response.status}.`,
          response.status,
          body,
        );
    }
  }
}
