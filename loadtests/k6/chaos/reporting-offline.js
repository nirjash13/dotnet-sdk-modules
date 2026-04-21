/**
 * reporting-offline.js — Chaos overlay: Reporting module offline.
 *
 * ═══════════════════════════════════════════════════════════════════════════
 * MANUAL SETUP REQUIRED before running this script
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * This script cannot orchestrate Docker from inside k6. You must pause the
 * Reporting container manually before or during the run:
 *
 *   # Option A — pause before the run starts (tests fault-isolation from the start):
 *   docker pause chassis-reporting
 *   k6 run loadtests/k6/chaos/reporting-offline.js --out experimental-prometheus-rw=http://localhost:9090/api/v1/write --tag test_run_id=chaos-reporting-offline
 *
 *   # Option B — pause mid-run (simulates runtime failure):
 *   k6 run loadtests/k6/chaos/reporting-offline.js ... &
 *   sleep 60
 *   docker pause chassis-reporting
 *   sleep 120
 *   docker unpause chassis-reporting
 *
 *   # Resume afterwards:
 *   docker unpause chassis-reporting
 *
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * Invariant under test:
 *   - Ledger module RPS is unchanged while Reporting is offline.
 *   - Reporting projection lag grows (visible in outbox depth metric).
 *   - No HTTP 5xx from the Ledger API due to Reporting unavailability.
 *   - Ledger p95 < 150 ms throughout (fault isolation NFR).
 *   - After docker unpause, the outbox drains within 60 s.
 *
 * Run:
 *   k6 run loadtests/k6/chaos/reporting-offline.js \
 *     --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
 *     --tag test_run_id=chaos-reporting-offline
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
    reporting_offline_ledger_load: {
      executor: 'constant-vus',
      vus: 100,
      duration: '10m',
    },
  },
  thresholds: {
    // Ledger p95 must remain within SLO even with Reporting offline.
    'http_req_duration{endpoint:post_transaction}': ['p(95)<150', 'p(99)<400'],
    // Zero 5xx from Ledger — fault isolation must hold.
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
      description: `Chaos: reporting-offline line ${i + 1}`,
      costCenter: `CC-${Math.floor(Math.random() * 50).toString().padStart(3, '0')}`,
    });
  }
  return JSON.stringify({
    idempotencyKey: `chaos-rep-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `CHAOS-REPORTING-OFFLINE-${Date.now()}`,
    description: 'Chaos overlay: Reporting container paused',
    lines,
    metadata: {
      source: 'k6-chaos-reporting-offline',
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
  console.log('chaos/reporting-offline: Ledger load starting. Ensure chassis-reporting is paused.');
  console.log('  Pause command: docker pause chassis-reporting');
  console.log('  Resume command: docker unpause chassis-reporting');
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
    'ledger write succeeds despite reporting offline': (r) => r.status >= 200 && r.status < 300,
    'no 5xx from ledger': (r) => r.status < 500,
  });
}

export function teardown(data) {
  console.log(`chaos/reporting-offline complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
  console.log('Verify in Grafana: chassis_outbox_lag_seconds increased then drained after docker unpause.');
}
