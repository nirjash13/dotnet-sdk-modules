/**
 * spike.js — Spike load scenario.
 *
 * Profile  : 20 VUs → 500 VUs in 10s, hold 5 min, ramp down to 20 VUs over 30s.
 * Endpoint : POST /api/v1/ledger/accounts/{aid}/transactions (2–4 KB JSON payload).
 * SLOs     : p95 < 150 ms, p99 < 400 ms, error rate < 0.5%.
 * Purpose  : Burst tolerance, connection-pool behaviour under sudden saturation.
 *
 * Run locally:
 *   k6 run loadtests/k6/scenarios/spike.js \
 *     --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
 *     --tag test_run_id=${TEST_RUN_ID}
 *
 * Env vars (all optional — defaults target local dev):
 *   BASE_URL, TENANT_ID, CLIENT_ID, CLIENT_SECRET, TEST_RUN_ID
 */

import http from 'k6/http';
import { check } from 'k6';
import { getToken, invalidateToken } from '../lib/auth.js';
import { buildHeaders } from '../lib/headers.js';

// ── Config ─────────────────────────────────────────────────────────────────
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

// ── k6 options ─────────────────────────────────────────────────────────────
export const options = {
  scenarios: {
    spike: {
      executor: 'ramping-vus',
      startVUs: 20,
      stages: [
        { duration: '10s', target: 500 },  // Spike: 20 → 500 in 10s
        { duration: '5m', target: 500 },   // Hold: 500 VUs for 5 minutes
        { duration: '30s', target: 20 },   // Ramp down: 500 → 20 in 30s
      ],
    },
  },
  thresholds: {
    'http_req_duration{endpoint:post_transaction}': ['p(95)<150', 'p(99)<400'],
    'http_req_failed': ['rate<0.005'],
  },
};

// ── Payload factory ─────────────────────────────────────────────────────────
function buildTransactionPayload() {
  const lineCount = Math.floor(Math.random() * 8) + 4;
  const lines = [];
  for (let i = 0; i < lineCount; i++) {
    lines.push({
      accountCode: `ACC-${(1000 + i).toString().padStart(6, '0')}`,
      debit: i % 2 === 0 ? (Math.random() * 10000).toFixed(2) : null,
      credit: i % 2 !== 0 ? (Math.random() * 10000).toFixed(2) : null,
      description: `Line item ${i + 1} — spike scenario line entry`,
      costCenter: `CC-${Math.floor(Math.random() * 100).toString().padStart(3, '0')}`,
      project: `PRJ-${Math.floor(Math.random() * 50).toString().padStart(4, '0')}`,
    });
  }
  return JSON.stringify({
    idempotencyKey: `spike-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `SPIKE-TEST-${Date.now()}`,
    description: 'Spike load test transaction — automated k6 scenario',
    lines,
    metadata: {
      source: 'k6-spike',
      testRunId: __ENV.TEST_RUN_ID || 'local',
      generatedAt: new Date().toISOString(),
    },
  });
}

// ── Test lifecycle ──────────────────────────────────────────────────────────

export function setup() {
  const token = getToken();
  const res = http.get(`${BASE_URL}/health`, {
    headers: { 'Authorization': `Bearer ${token}` },
  });
  if (res.status !== 200) {
    throw new Error(`Health check failed: HTTP ${res.status}. Is the stack up?`);
  }
  console.log('Spike setup: health check passed. Ramping 20 → 500 VUs in 10s.');
  return { startTime: new Date().toISOString() };
}

export default function () {
  let token = getToken();

  const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 1000).toString().padStart(12, '0')}`;
  const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}/transactions`;
  const payload = buildTransactionPayload();

  const res = http.post(url, payload, {
    headers: buildHeaders(token),
    tags: { endpoint: 'post_transaction' },
  });

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
}

export function teardown(data) {
  console.log(`Spike complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
}
