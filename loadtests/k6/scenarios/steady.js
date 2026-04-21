/**
 * steady.js - Steady-state load scenario.
 *
 * Profile  : 100 VUs constant for 15 minutes.
 * Endpoint : POST /api/v1/ledger/accounts/{accountId}/transactions
 * SLOs     : p95 < 150 ms, p99 < 400 ms, error rate < 0.5%.
 */

import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const ACCOUNT_POOL_SIZE = Number(__ENV.ACCOUNT_POOL_SIZE || '1000');
const TENANT_ID = __ENV.TENANT_ID || '00000000-0000-0000-0000-000000000001';

export const options = {
  scenarios: {
    steady_state: {
      executor: 'constant-vus',
      vus: 100,
      duration: '15m',
    },
  },
  thresholds: {
    'http_req_duration{endpoint:post_transaction}': ['p(95)<150', 'p(99)<400'],
    'http_req_failed': ['rate<0.005'],
  },
};

function buildTransactionPayload() {
  return JSON.stringify({
    amount: Number((Math.random() * 1000 + 1).toFixed(2)),
    currency: 'USD',
    memo: 'Steady-state load test transaction',
  });
}

function pickAccountId() {
  const n = Math.floor(Math.random() * ACCOUNT_POOL_SIZE);
  return `00000000-0000-0000-0000-${n.toString().padStart(12, '0')}`;
}

function buildHeaders() {
  return {
    'X-Tenant-Id': TENANT_ID,
    'Content-Type': 'application/json',
    Accept: 'application/json',
  };
}

export function setup() {
  const res = http.get(`${BASE_URL}/health`);

  if (res.status !== 200) {
    throw new Error(`Health check failed: HTTP ${res.status}. Is the stack up?`);
  }
}

export default function () {
  const accountId = pickAccountId();
  const url = `${BASE_URL}/api/v1/ledger/accounts/${accountId}/transactions`;
  const payload = buildTransactionPayload();

  const res = http.post(url, payload, {
    headers: buildHeaders(),
    tags: { endpoint: 'post_transaction' },
  });

  check(res, {
    'status is 2xx': (r) => r.status >= 200 && r.status < 300,
  });
}
