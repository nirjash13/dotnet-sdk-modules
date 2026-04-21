/**
 * postgres-starved.js — Chaos overlay: Postgres connection pool exhaustion.
 *
 * ═══════════════════════════════════════════════════════════════════════════
 * MANUAL SETUP REQUIRED
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * Pool exhaustion is injected by setting a low Npgsql max pool size on the
 * chassis host. Set the environment variable before starting the service:
 *
 *   # In your .env or docker-compose.override.yml:
 *   Npgsql__MaxPoolSize=5
 *
 *   # Or pass directly to the dotnet run command:
 *   Npgsql__MaxPoolSize=5 dotnet run --project src/Chassis.Host
 *
 *   # Then run this scenario (50 VUs >> 5 pool connections → exhaustion):
 *   k6 run loadtests/k6/chaos/postgres-starved.js \
 *     --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
 *     --tag test_run_id=chaos-postgres-starved
 *
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * Invariants under test:
 *   - Under pool exhaustion, the API returns HTTP 503 with ProblemDetails
 *     body containing code: "pool_exhausted".
 *   - The API must NOT hang indefinitely (Npgsql connection timeout enforced).
 *   - No unhandled exceptions — all errors return structured ProblemDetails.
 *   - 503 responses are expected; 5xx OTHER than 503 are failures.
 *
 * Env vars: BASE_URL, TENANT_ID, CLIENT_ID, CLIENT_SECRET, TEST_RUN_ID
 */

import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';
import { getToken, invalidateToken } from '../lib/auth.js';
import { buildHeaders } from '../lib/headers.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

// Track the rate of expected 503s separately from true failures.
const poolExhaustedRate = new Rate('pool_exhausted_503_rate');
const unexpectedErrorRate = new Rate('unexpected_5xx_rate');

export const options = {
  scenarios: {
    pool_starved: {
      executor: 'constant-vus',
      // 50 VUs against a 5-connection pool — guarantees exhaustion.
      vus: 50,
      duration: '5m',
    },
  },
  thresholds: {
    // 503 pool_exhausted is expected — but no other 5xx allowed.
    'unexpected_5xx_rate': ['rate<0.01'],
    // Response time must be bounded — no unbounded waits.
    'http_req_duration{endpoint:post_transaction}': ['p(99)<10000'],
  },
};

function buildTransactionPayload() {
  return JSON.stringify({
    idempotencyKey: `chaos-pool-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `CHAOS-POOL-STARVED-${Date.now()}`,
    description: 'Chaos overlay: Postgres pool exhaustion test',
    lines: [
      {
        accountCode: 'ACC-001000',
        debit: '100.00',
        credit: null,
        description: 'Pool exhaustion test line',
      },
    ],
    metadata: {
      source: 'k6-chaos-postgres-starved',
      testRunId: __ENV.TEST_RUN_ID || 'chaos-local',
    },
  });
}

export function setup() {
  console.log('chaos/postgres-starved: Starting 50 VU pool exhaustion test.');
  console.log('  Prerequisite: Start chassis host with Npgsql__MaxPoolSize=5');
  console.log('  Expected: HTTP 503 with ProblemDetails code=pool_exhausted');
  console.log('  NOT expected: Unbounded waits, 500 Internal Server Error, or hangs.');

  const token = getToken();
  // Don't fail setup if health returns 503 — the DB might already be starved.
  const res = http.get(`${BASE_URL}/health`, {
    headers: { 'Authorization': `Bearer ${token}` },
  });
  console.log(`Health check status: ${res.status} (503 expected under pool exhaustion)`);
  return { startTime: new Date().toISOString() };
}

export default function () {
  let token = getToken();
  const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 100).toString().padStart(12, '0')}`;
  const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}/transactions`;
  const payload = buildTransactionPayload();

  const res = http.post(url, payload, {
    headers: buildHeaders(token),
    tags: { endpoint: 'post_transaction' },
    // Enforce a client-side timeout — if pool exhaustion causes unbounded wait, fail fast.
    timeout: '15s',
  });

  if (res.status === 401) {
    invalidateToken();
    token = getToken();
    http.post(url, payload, {
      headers: buildHeaders(token),
      tags: { endpoint: 'post_transaction' },
      timeout: '15s',
    });
  }

  const is503 = res.status === 503;
  const isOther5xx = res.status >= 500 && res.status !== 503;

  poolExhaustedRate.add(is503);
  unexpectedErrorRate.add(isOther5xx);

  if (is503) {
    // Verify the 503 is structured as expected ProblemDetails.
    let body;
    try {
      body = JSON.parse(res.body);
    } catch (_) {
      body = null;
    }
    check(res, {
      '503 has ProblemDetails body': () => body !== null && typeof body === 'object',
      '503 has pool_exhausted code': () => body && body.code === 'pool_exhausted',
    });
  } else {
    check(res, {
      'non-503 status is 2xx': (r) => r.status >= 200 && r.status < 300,
    });
  }
}

export function teardown(data) {
  console.log(`chaos/postgres-starved complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
  console.log('Check Grafana: http_req_duration p99 must be bounded (< 10s), not unbounded.');
  console.log('Restore pool size: unset Npgsql__MaxPoolSize and restart the chassis host.');
}
