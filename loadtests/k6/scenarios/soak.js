/**
 * soak.js — Soak / endurance load scenario.
 *
 * Profile  : 50 VUs constant for 4 hours — mixed reads + writes.
 * Purpose  : Detect memory leaks, connection-pool drift, outbox accumulation over time.
 * SLOs     : p95 < 150 ms (writes), p95 < 80 ms (reads), error rate < 0.5%.
 *
 * Run locally (starts a 4-hour run — use tmux or nohup):
 *   k6 run loadtests/k6/scenarios/soak.js \
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
    soak: {
      executor: 'constant-vus',
      vus: 50,
      duration: '4h',
    },
  },
  thresholds: {
    'http_req_duration{endpoint:post_transaction}': ['p(95)<150', 'p(99)<400'],
    'http_req_duration{endpoint:get_account}': ['p(95)<80'],
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
      description: `Line item ${i + 1} — soak scenario entry`,
      costCenter: `CC-${Math.floor(Math.random() * 100).toString().padStart(3, '0')}`,
      project: `PRJ-${Math.floor(Math.random() * 50).toString().padStart(4, '0')}`,
    });
  }
  return JSON.stringify({
    idempotencyKey: `soak-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `SOAK-TEST-${Date.now()}`,
    description: 'Soak load test transaction — automated k6 scenario',
    lines,
    metadata: {
      source: 'k6-soak',
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
  console.log('Soak setup: health check passed. Starting 50 VU constant load for 4h.');
  return { startTime: new Date().toISOString() };
}

export default function () {
  let token = getToken();
  const headers = buildHeaders(token);

  // Mix: 70% writes, 30% reads — soak scenario stresses the outbox and read paths.
  const isWrite = Math.random() < 0.7;

  if (isWrite) {
    const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 1000).toString().padStart(12, '0')}`;
    const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}/transactions`;
    const payload = buildTransactionPayload();

    const res = http.post(url, payload, {
      headers,
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
      'write status is 2xx': (r) => r.status >= 200 && r.status < 300,
    });
  } else {
    const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 1000).toString().padStart(12, '0')}`;
    const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}`;

    const res = http.get(url, {
      headers,
      tags: { endpoint: 'get_account' },
    });

    if (res.status === 401) {
      invalidateToken();
      token = getToken();
      http.get(url, {
        headers: buildHeaders(token),
        tags: { endpoint: 'get_account' },
      });
    }

    check(res, {
      'read status is 2xx or 404': (r) => r.status >= 200 && r.status < 300 || r.status === 404,
    });
  }
}

export function teardown(data) {
  console.log(`Soak complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
}
