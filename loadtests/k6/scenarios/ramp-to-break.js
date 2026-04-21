/**
 * ramp-to-break.js — Ramp-to-break scenario.
 *
 * Profile  : 10 → 2000 VUs linear over 30 minutes.
 * Purpose  : Find the system's breaking point — records the VU count at which p95
 *            latency crosses 500 ms (the "knee").
 * Metric   : Custom metric `knee_vus` — emitted once when p95 > 500 ms is first observed.
 *
 * Run locally:
 *   k6 run loadtests/k6/scenarios/ramp-to-break.js \
 *     --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
 *     --tag test_run_id=${TEST_RUN_ID}
 *
 * Env vars (all optional — defaults target local dev):
 *   BASE_URL, TENANT_ID, CLIENT_ID, CLIENT_SECRET, TEST_RUN_ID
 *
 * Note: This scenario intentionally drives the system beyond SLO; thresholds
 * are set as 'abortOnFail: false' so the run completes and records the knee point.
 */

import http from 'k6/http';
import { check } from 'k6';
import { Gauge } from 'k6/metrics';
import { getToken, invalidateToken } from '../lib/auth.js';
import { buildHeaders } from '../lib/headers.js';

// ── Custom metrics ─────────────────────────────────────────────────────────
// knee_vus records the VU count at which p95 latency first exceeded 500ms.
// Emitted as a Gauge so it appears as a single value in Prometheus.
const kneeVus = new Gauge('knee_vus');

// ── Config ─────────────────────────────────────────────────────────────────
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const KNEE_THRESHOLD_MS = 500; // p95 latency at which we consider the knee reached

// Module-level state — track whether the knee has been recorded already.
let _kneeRecorded = false;
let _windowSamples = [];
const WINDOW_SIZE = 100; // samples to compute rolling p95

// ── k6 options ─────────────────────────────────────────────────────────────
export const options = {
  scenarios: {
    ramp_to_break: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '30m', target: 2000 }, // Linear ramp 10 → 2000 over 30 min
      ],
    },
  },
  thresholds: {
    // Non-aborting: we want to continue to find the break point even after SLO is breached.
    'http_req_duration{endpoint:post_transaction}': [
      { threshold: 'p(95)<150', abortOnFail: false },
      { threshold: 'p(99)<400', abortOnFail: false },
    ],
    'http_req_failed': [{ threshold: 'rate<0.005', abortOnFail: false }],
    // knee_vus > 0 means we found the knee — CI can read this value from Prometheus.
    'knee_vus': [{ threshold: 'value>0', abortOnFail: false }],
  },
};

// ── Helpers ─────────────────────────────────────────────────────────────────
/** Compute the p95 of a sample array. */
function computeP95(samples) {
  if (samples.length === 0) return 0;
  const sorted = samples.slice().sort((a, b) => a - b);
  const idx = Math.ceil(0.95 * sorted.length) - 1;
  return sorted[Math.max(0, idx)];
}

// ── Payload factory ─────────────────────────────────────────────────────────
function buildTransactionPayload() {
  const lineCount = Math.floor(Math.random() * 8) + 4;
  const lines = [];
  for (let i = 0; i < lineCount; i++) {
    lines.push({
      accountCode: `ACC-${(1000 + i).toString().padStart(6, '0')}`,
      debit: i % 2 === 0 ? (Math.random() * 10000).toFixed(2) : null,
      credit: i % 2 !== 0 ? (Math.random() * 10000).toFixed(2) : null,
      description: `Line item ${i + 1} — ramp-to-break scenario`,
      costCenter: `CC-${Math.floor(Math.random() * 100).toString().padStart(3, '0')}`,
      project: `PRJ-${Math.floor(Math.random() * 50).toString().padStart(4, '0')}`,
    });
  }
  return JSON.stringify({
    idempotencyKey: `rtb-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `RAMP-TO-BREAK-${Date.now()}`,
    description: 'Ramp-to-break load test — automated k6 scenario',
    lines,
    metadata: {
      source: 'k6-ramp-to-break',
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
  console.log('Ramp-to-break setup: health check passed. Ramping 10 → 2000 VUs over 30 min.');
  return { startTime: new Date().toISOString() };
}

export default function () {
  let token = getToken();

  const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 1000).toString().padStart(12, '0')}`;
  const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}/transactions`;
  const payload = buildTransactionPayload();

  const start = Date.now();
  const res = http.post(url, payload, {
    headers: buildHeaders(token),
    tags: { endpoint: 'post_transaction' },
  });
  const duration = Date.now() - start;

  if (res.status === 401) {
    invalidateToken();
    token = getToken();
    http.post(url, payload, {
      headers: buildHeaders(token),
      tags: { endpoint: 'post_transaction' },
    });
  }

  check(res, {
    'status is 2xx or 503': (r) => (r.status >= 200 && r.status < 300) || r.status === 503,
  });

  // Rolling p95 knee detection — record when first crossed.
  if (!_kneeRecorded) {
    _windowSamples.push(duration);
    if (_windowSamples.length > WINDOW_SIZE) {
      _windowSamples.shift();
    }
    if (_windowSamples.length >= 20) {
      const p95 = computeP95(_windowSamples);
      if (p95 > KNEE_THRESHOLD_MS) {
        // __VU is the current VU number; use as a proxy for active VU count.
        const currentVus = __VU;
        kneeVus.add(currentVus);
        _kneeRecorded = true;
        console.log(
          `KNEE DETECTED: p95=${p95.toFixed(1)}ms exceeded ${KNEE_THRESHOLD_MS}ms at approximately ${currentVus} VUs`
        );
      }
    }
  }
}

export function teardown(data) {
  console.log(`Ramp-to-break complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
  if (!_kneeRecorded) {
    console.log(`No knee detected — system p95 stayed below ${KNEE_THRESHOLD_MS}ms at all VU levels up to 2000.`);
  }
}
