/**
 * headers.js — builds standard HTTP headers for chassis API requests.
 *
 * Produces:
 *   Authorization: Bearer <token>
 *   X-Tenant-Id: <tenantId>
 *   Content-Type: application/json
 *
 * Usage:
 *   import { buildHeaders } from './lib/headers.js';
 *   const headers = buildHeaders(token);
 */

const TENANT_ID = __ENV.TENANT_ID || '00000000-0000-0000-0000-000000000001';

/**
 * Builds request headers for authenticated chassis API calls.
 * @param {string} token - Bearer token returned by getToken().
 * @returns {Object} Headers object suitable for k6 http.* calls.
 */
export function buildHeaders(token) {
  return {
    'Authorization': `Bearer ${token}`,
    'X-Tenant-Id': TENANT_ID,
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };
}

/**
 * Returns the configured tenant ID (from TENANT_ID env var or default).
 * @returns {string} UUID string.
 */
export function tenantId() {
  return TENANT_ID;
}
