/**
 * auth.js — JWT acquisition helper for k6 scenarios.
 *
 * Acquires a token from the OpenIddict token endpoint using client_credentials flow.
 * The token is cached at module level; a 401 response from the API triggers a re-fetch.
 *
 * Usage:
 *   import { getToken } from './lib/auth.js';
 *   const token = getToken();
 */

import http from 'k6/http';

// Module-level cache — shared across VUs within the same scenario instance.
let _cachedToken = null;
let _tokenExpiresAt = 0;

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const CLIENT_ID = __ENV.CLIENT_ID || 'chassis-loadtest';
const CLIENT_SECRET = __ENV.CLIENT_SECRET || 'chassis-loadtest-secret';
const OIDC_SCOPE = __ENV.OIDC_SCOPE || 'chassis-api';

/**
 * Fetch a new token from the /connect/token endpoint.
 * @returns {string} The access token string.
 */
function fetchToken() {
  const url = `${BASE_URL}/connect/token`;
  const payload = {
    grant_type: 'client_credentials',
    client_id: CLIENT_ID,
    client_secret: CLIENT_SECRET,
    scope: OIDC_SCOPE,
  };

  const res = http.post(url, payload, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    tags: { endpoint: 'token' },
  });

  if (res.status !== 200) {
    throw new Error(
      `Token acquisition failed: HTTP ${res.status} — ${res.body}`
    );
  }

  const body = JSON.parse(res.body);
  if (!body.access_token) {
    throw new Error('Token response missing access_token field');
  }

  // Cache expiry: use expires_in minus a 30-second safety margin.
  const expiresIn = body.expires_in || 3600;
  _tokenExpiresAt = Date.now() + (expiresIn - 30) * 1000;
  _cachedToken = body.access_token;

  return _cachedToken;
}

/**
 * Returns a valid JWT. Fetches from /connect/token on first call or when the
 * cached token is within 30 seconds of expiry.
 * Call invalidateToken() when the API returns 401.
 * @returns {string} Bearer token.
 */
export function getToken() {
  if (_cachedToken === null || Date.now() >= _tokenExpiresAt) {
    return fetchToken();
  }
  return _cachedToken;
}

/**
 * Clears the cached token. Call this when a request returns 401 so the next
 * call to getToken() re-fetches a fresh credential.
 */
export function invalidateToken() {
  _cachedToken = null;
  _tokenExpiresAt = 0;
}
