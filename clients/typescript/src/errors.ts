/** Base error for all SaasBuilder client errors. */
export class SaasBuilderError extends Error {
  constructor(
    message: string,
    public readonly statusCode?: number,
    public readonly body?: unknown,
  ) {
    super(message);
    this.name = "SaasBuilderError";
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

/** Thrown when the server returns 401 and no refresh token is available. */
export class UnauthorizedError extends SaasBuilderError {
  constructor(body?: unknown) {
    super("Unauthorized — token missing or expired.", 401, body);
    this.name = "UnauthorizedError";
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

/** Thrown when the server requires MFA verification before the request can proceed. */
export class MfaRequiredError extends SaasBuilderError {
  constructor(public readonly mfaToken: string) {
    super("MFA verification required.", 200);
    this.name = "MfaRequiredError";
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

/** Thrown when the server returns a 403 Forbidden response. */
export class ForbiddenError extends SaasBuilderError {
  constructor(body?: unknown) {
    super("Forbidden — insufficient permissions.", 403, body);
    this.name = "ForbiddenError";
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

/** Thrown when the server returns a 404 Not Found response. */
export class NotFoundError extends SaasBuilderError {
  constructor(resource?: string) {
    super(resource ? `${resource} not found.` : "Resource not found.", 404);
    this.name = "NotFoundError";
    Object.setPrototypeOf(this, new.target.prototype);
  }
}

/** Thrown when the server returns a 422 or 400 validation error. */
export class ValidationError extends SaasBuilderError {
  constructor(
    public readonly errors: Record<string, string[]>,
    body?: unknown,
  ) {
    super("Validation failed.", 400, body);
    this.name = "ValidationError";
    Object.setPrototypeOf(this, new.target.prototype);
  }
}
