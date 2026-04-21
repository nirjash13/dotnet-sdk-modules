/**
 * steady.js — Steady-state load scenario.
 *
 * Profile  : 100 VUs constant for 15 minutes → ~300 RPS.
 * Endpoint : POST /api/v1/ledger/accounts/{aid}/transactions (2–4 KB JSON payload).
 * SLOs     : p95 < 150 ms, p99 < 400 ms, error rate < 0.5%.
 *
 * Run locally:
 *   k6 run loadtests/k6/scenarios/steady.js \
 *     --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
 *     --tag test_run_id=${TEST_RUN_ID}
 *
 * Env vars (all optional — defaults target local dev):
 *   BASE_URL       e.g. http://localhost:5000
 *   TENANT_ID      UUID of the test tenant
 *   CLIENT_ID      OIDC client ID
 *   CLIENT_SECRET  OIDC client secret
 *   TEST_RUN_ID    Unique tag for this run (set by CI; defaults to "local")
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';
import { getToken, invalidateToken } from '../lib/auth.js';
import { buildHeaders } from '../lib/headers.js';

// ── Custom metrics ─────────────────────────────────────────────────────────
const postTransactionDuration = new Trend('http_req_duration', true);
const postTransactionErrors = new Rate('http_req_failed');

// ── Config ─────────────────────────────────────────────────────────────────
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

// ── k6 options ─────────────────────────────────────────────────────────────
export const options = {
  scenarios: {
    steady_state: {
      executor: 'constant-vus',
      vus: 100,
      duration: '15m',
    },
  },
  thresholds: {
    // Tag-scoped thresholds on the post_transaction endpoint
    'http_req_duration{endpoint:post_transaction}': ['p(95)<150', 'p(99)<400'],
    'http_req_failed': ['rate<0.005'],
  },
};

// ── Payload factory ─────────────────────────────────────────────────────────
/** Returns a 2–4 KB JSON body for a Ledger transaction. */
function buildTransactionPayload() {
  const lineCount = Math.floor(Math.random() * 8) + 4; // 4–12 line items
  const lines = [];
  for (let i = 0; i < lineCount; i++) {
    lines.push({
      accountCode: `ACC-${(1000 + i).toString().padStart(6, '0')}`,
      debit: i % 2 === 0 ? (Math.random() * 10000).toFixed(2) : null,
      credit: i % 2 !== 0 ? (Math.random() * 10000).toFixed(2) : null,
      description: `Line item ${i + 1} — steady-state load test entry for scenario validation`,
      costCenter: `CC-${Math.floor(Math.random() * 100).toString().padStart(3, '0')}`,
      project: `PRJ-${Math.floor(Math.random() * 50).toString().padStart(4, '0')}`,
    });
  }
  return JSON.stringify({
    idempotencyKey: `steady-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `STEADY-STATE-TEST-${Date.now()}`,
    description: 'Steady-state load test transaction — automated k6 scenario',
    lines,
    metadata: {
      source: 'k6-steady-state',
      testRunId: __ENV.TEST_RUN_ID || 'local',
      generatedAt: new Date().toISOString(),
    },
  });
}

// ── Test lifecycle ──────────────────────────────────────────────────────────

export function setup() {
  // Pre-warm: verify the stack is up and token works before sending load.
  const token = getToken();
  const res = http.get(`${BASE_URL}/health`, {
    headers: { 'Authorization': `Bearer ${token}` },
  });
  if (res.status !== 200) {
    throw new Error(`Health check failed: HTTP ${res.status}. Is the stack up?`);
  }
  console.log('Steady-state setup: health check passed, starting 100 VU constant load.');
  return { startTime: new Date().toISOString() };
}

export default function () {
  let token = getToken();

  // Fixed account ID for this VU iteration — in real scenarios, pick from a seeded pool.
  const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 1000).toString().padStart(12, '0')}`;
  const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}/transactions`;
  const payload = buildTransactionPayload();

  const res = http.post(url, payload, {
    headers: buildHeaders(token),
    tags: { endpoint: 'post_transaction' },
  });

  // Re-authenticate on 401 and retry once.
  if (res.status === 401) {
    invalidateToken();
    token = getToken();
    http.post(url, payload, {
      headers: buildHeaders(token),
      tags: { endpoint: 'post_transaction' },
    });
  }

  check(res, {
    'status is 2xx': (r) => r.status >= 200 && r.status < 300,
    'response has body': (r) => r.body && r.body.length > 0,
  });

  // No sleep: constant-vus executor controls concurrency; VUs iterate continuously.
}

export function teardown(data) {
  console.log(`Steady-state complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
}
