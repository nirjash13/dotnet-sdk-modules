/**
 * network-latency.js — Chaos overlay: 50 ms network latency injected between
 * the chassis host and RabbitMQ.
 *
 * ═══════════════════════════════════════════════════════════════════════════
 * MANUAL SETUP REQUIRED
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * This script cannot inject network latency from inside k6. Use Linux Traffic
 * Control (tc netem) on the RabbitMQ container before running this script.
 *
 * Step 1 — find the RabbitMQ container's network interface:
 *   docker inspect chassis-rabbitmq | grep -i '"IPAddress"'
 *
 * Step 2 — exec into the RabbitMQ container and inject latency:
 *   docker exec -it chassis-rabbitmq bash
 *   apt-get install -y iproute2   # if not present
 *   tc qdisc add dev eth0 root netem delay 50ms
 *   exit
 *
 * Step 3 — run this scenario:
 *   k6 run loadtests/k6/chaos/network-latency.js \
 *     --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
 *     --tag test_run_id=chaos-network-latency
 *
 * Step 4 — remove the latency after the run:
 *   docker exec chassis-rabbitmq tc qdisc del dev eth0 root netem
 *
 * Alternative — use Pumba or toxiproxy for more ergonomic network fault injection.
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * Invariants under test:
 *   - With 50 ms added latency on the Rabbit connection, saga p99 stays within 2 s.
 *   - Ledger writes (HTTP) p95 remain < 150 ms (outbox write is local — unaffected).
 *   - Saga completion (end-to-end) p99 < 2000 ms despite bus latency.
 *   - No HTTP 5xx — the host degrades gracefully.
 *
 * Note: Saga completion time is measured indirectly here via the Ledger API
 * response time. For true saga e2e measurement, instrument the saga via
 * chassis_saga_duration_seconds (see Grafana: saga-health.json).
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
    network_latency: {
      executor: 'constant-vus',
      vus: 50,
      duration: '10m',
    },
  },
  thresholds: {
    // Ledger write p95 must remain within SLO (outbox path is local — unaffected by Rabbit latency).
    'http_req_duration{endpoint:post_transaction}': ['p(95)<150', 'p(99)<400'],
    // Saga e2e budget — p99 < 2000ms even with +50ms bus latency.
    // Measured via the post_transaction endpoint which triggers saga initiation.
    'http_req_duration{endpoint:saga_transaction}': ['p(99)<2000'],
    'http_req_failed': ['rate<0.005'],
  },
};

function buildTransactionPayload(sagaTrigger) {
  const lines = [
    {
      accountCode: 'ACC-001001',
      debit: (Math.random() * 5000).toFixed(2),
      credit: null,
      description: `Network latency chaos line — saga trigger: ${sagaTrigger}`,
    },
    {
      accountCode: 'ACC-001002',
      debit: null,
      credit: (Math.random() * 5000).toFixed(2),
      description: 'Offsetting credit entry',
    },
  ];
  return JSON.stringify({
    idempotencyKey: `chaos-net-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
    transactionDate: new Date().toISOString().split('T')[0],
    currency: 'USD',
    reference: `CHAOS-NET-LATENCY-${Date.now()}`,
    description: 'Chaos overlay: 50ms network latency on RabbitMQ',
    lines,
    metadata: {
      source: 'k6-chaos-network-latency',
      testRunId: __ENV.TEST_RUN_ID || 'chaos-local',
      sagaTrigger: sagaTrigger ? 'yes' : 'no',
    },
  });
}

export function setup() {
  console.log('chaos/network-latency: Starting 50 VU load with expected 50ms Rabbit latency.');
  console.log('  Inject: docker exec chassis-rabbitmq tc qdisc add dev eth0 root netem delay 50ms');
  console.log('  Remove: docker exec chassis-rabbitmq tc qdisc del dev eth0 root netem');
  console.log('  Invariant: saga p99 < 2000ms, Ledger write p95 < 150ms.');

  const token = getToken();
  const res = http.get(`${BASE_URL}/health`, {
    headers: { 'Authorization': `Bearer ${token}` },
  });
  if (res.status !== 200) {
    throw new Error(`Health check failed: HTTP ${res.status}`);
  }
  return { startTime: new Date().toISOString() };
}

export default function () {
  let token = getToken();
  const headers = buildHeaders(token);

  // 80% normal writes (outbox path — unaffected by Rabbit latency).
  // 20% saga-triggering writes (round-trip through bus — affected by latency).
  const isSagaTrigger = Math.random() < 0.2;
  const accountId = `00000000-0000-0000-0000-${Math.floor(Math.random() * 200).toString().padStart(12, '0')}`;
  const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}/transactions`;
  const tag = isSagaTrigger ? 'saga_transaction' : 'post_transaction';
  const payload = buildTransactionPayload(isSagaTrigger);

  const res = http.post(url, payload, {
    headers,
    tags: { endpoint: tag },
  });

  if (res.status === 401) {
    invalidateToken();
    token = getToken();
    http.post(url, payload, {
      headers: buildHeaders(token),
      tags: { endpoint: tag },
    });
  }

  check(res, {
    'write succeeds under network latency': (r) => r.status >= 200 && r.status < 300,
    'no 5xx under latency': (r) => r.status < 500,
  });
}

export function teardown(data) {
  console.log(`chaos/network-latency complete. Started: ${data.startTime}, finished: ${new Date().toISOString()}`);
  console.log('Remove latency: docker exec chassis-rabbitmq tc qdisc del dev eth0 root netem');
  console.log('Verify in Grafana saga-health.json: chassis_saga_duration_seconds p99 < 2000ms.');
}
