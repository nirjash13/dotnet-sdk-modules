/**
 * rabbit-paused.js — Chaos overlay: RabbitMQ paused mid-scenario.
 *
 * ═══════════════════════════════════════════════════════════════════════════
 * MANUAL SETUP REQUIRED
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * This script cannot orchestrate Docker from inside k6. Run the scenario
 * and pause/unpause RabbitMQ manually to inject the fault mid-run:
 *
 *   # Terminal 1 — start the load test:
 *   k6 run loadtests/k6/chaos/rabbit-paused.js \
 *     --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
 *     --tag test_run_id=chaos-rabbit-paused
 *
 *   # Terminal 2 — pause RabbitMQ after ~60s of steady load:
 *   sleep 60
 *   docker pause chassis-rabbitmq
 *
 *   # Wait 2 minutes (outbox should buffer writes without any 5xx):
 *   sleep 120
 *
 *   # Unpause (outbox must drain within 60s):
 *   docker unpause chassis-rabbitmq
 *
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * Invariants under test:
 *   - Ledger writes continue (HTTP 2xx) while RabbitMQ is paused.
 *   - No HTTP 5xx spike during the pause window (outbox absorbs messages).
 *   - Ledger p95 remains < 150 ms throughout (outbox write is local Postgres).
 *   - After docker unpause, the outbox drains within 60 s.
 *     Check: chassis_outbox_lag_seconds returns to baseline.
 *
 * Env vars: BASE_URL, TENANT_ID, CLIENT_ID, CLIENT_SECRET, TEST_RUN_ID
 */

import http from 'k6/http';
import { check } from 'k6';
import { getToken, invalidateToken } from '../lib/auth.js';
import { buildHeaders } from '../lib/headers.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  scenarios: {
    rabbit_paused_writes: {
      executor: 'constant-vus',
      vus: 100,
      duration: '10m',
    },
  },
  thresholds: {
    // Ledger writes must succeed via outbox even with RabbitMQ paused.
    'http_req_duration{endpoint:post_transaction}': ['p(95)<150', 'p(99)<400'],
    // Outbox absorbs messages — no 5xx spike expected.
    'http_req_failed': ['rate<0.005'],
  },
};

function buildTransactionPayload() {
  const lineCount = Math.floor(Math.random() * 6) + 4;
  const lines = [];
  for (let i = 0; i < lineCount; i++) {
    lines.push({
      accountCode: `ACC-${(1000 + i).toString().padStart(6, '0')}`,
      debit: i % 2 === 0 ? (Math.random() * 5000).toFixed(2) : null,
      credit: i % 2 !== 0 ? (Math.random() * 5000).toFixed(2) : null,
      description: `Chaos: rabbit-paused line ${i + 1}`,
      costCenter: `CC-${Math.floor(Math.random() * 50).toString().padStart(3, '0')}`,
    });
  }
  return JSON.stringify({
    idempotencyKey: `chaos-rab-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `CHAOS-RABBIT-PAUSED-${Date.now()}`,
    description: 'Chaos overlay: RabbitMQ container paused mid-run',
    lines,
    metadata: {
      source: 'k6-chaos-rabbit-paused',
      testRunId: __ENV.TEST_RUN_ID || 'chaos-local',
    },
  });
}

export function setup() {
  const token = getToken();
  const res = http.get(`${BASE_URL}/health`, {
    headers: { 'Authorization': `Bearer ${token}` },
  });
  if (res.status !== 200) {
    throw new Error(`Health check failed: HTTP ${res.status}`);
  }
  console.log('chaos/rabbit-paused: Ledger load starting at 100 VUs.');
  console.log('  After ~60s: docker pause chassis-rabbitmq');
  console.log('  After ~180s: docker unpause chassis-rabbitmq');
  console.log('  Verify: chassis_outbox_lag_seconds grows then drains within 60s of unpause.');
  return { startTime: new Date().toISOString() };
}

export default function () {
  let token = getToken();
  const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 500).toString().padStart(12, '0')}`;
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
    'write succeeds while rabbit paused (outbox path)': (r) => r.status >= 200 && r.status < 300,
    'no 5xx during rabbit pause': (r) => r.status < 500,
  });
}

export function teardown(data) {
  console.log(`chaos/rabbit-paused complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
  console.log('In Grafana: check chassis_outbox_lag_seconds — should return to baseline after unpause.');
}
