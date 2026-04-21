# Load Tests — Modular SaaS Chassis

End-to-end load testing for the Chassis SaaS platform. Uses k6 as the primary
load generator and NBomber for .NET in-process micro-benchmarks.

## Tool choices

| Tool | Role | Why |
|---|---|---|
| **k6** | Primary load generator | Native Prometheus remote-write; JS scripts portable across teams; Grafana integration |
| **NBomber 6.x** | .NET micro-benchmarks | In-process dispatcher overhead measurement (mediator vs bus); stays in .NET test assets |

## Directory structure

```
loadtests/
├── k6/
│   ├── lib/
│   │   ├── auth.js          — JWT acquisition (client_credentials) with caching
│   │   └── headers.js       — X-Tenant-Id + Authorization header builder
│   ├── scenarios/
│   │   ├── steady.js        — 100 VUs × 15 min (~300 RPS); CI uses 5-min variant
│   │   ├── spike.js         — 20 → 500 VUs in 10s, hold 5 min, ramp down
│   │   ├── soak.js          — 50 VUs × 4 hours; mixed reads + writes
│   │   └── ramp-to-break.js — 10 → 2000 VUs over 30 min; emits knee_vus metric
│   └── chaos/
│       ├── reporting-offline.js  — Ledger writes while Reporting is paused
│       ├── rabbit-paused.js      — Outbox buffering while RabbitMQ is paused
│       ├── postgres-starved.js   — Pool exhaustion (Npgsql__MaxPoolSize=5 + 50 VUs)
│       └── network-latency.js   — 50ms tc netem on Rabbit; saga p99 budget test
├── nbomber/
│   └── Dispatch.Benchmarks/ — NBomber project (not in Chassis.sln by default)
├── slo.yaml                 — Machine-readable SLO definitions (mirrors k6 thresholds)
└── README.md                — This file
```

## SLO definitions

`slo.yaml` is the single source of truth for SLO targets. k6 scenario
thresholds mirror these values — if you update one, update the other.

| SLO | Target |
|---|---|
| POST /api/v1/ledger/…/transactions p95 | < 150 ms |
| POST /api/v1/ledger/…/transactions p99 | < 400 ms |
| GET /api/v1/ledger/accounts/{id} p95 | < 80 ms |
| Auth token endpoint p95 | < 250 ms |
| Saga completion p99 | < 2 s |
| Outbox lag p99 | < 500 ms |
| HTTP 5xx error rate | < 0.5% |
| Sustained throughput | ≥ 500 RPS |

## Env vars (all scenarios)

| Variable | Default | Description |
|---|---|---|
| `BASE_URL` | `http://localhost:5000` | Chassis host base URL |
| `TENANT_ID` | `00000000-0000-0000-0000-000000000001` | Tenant UUID for X-Tenant-Id header |
| `CLIENT_ID` | `chassis-loadtest` | OIDC client ID for token acquisition |
| `CLIENT_SECRET` | `chassis-loadtest-secret` | OIDC client secret |
| `TEST_RUN_ID` | `local` | Tag written to Prometheus via remote-write for run isolation |

## Run locally

### Prerequisites

1. Install k6: https://k6.io/docs/get-started/installation/
2. Start the observability stack: `docker compose -f deploy/docker-compose.yml up -d`
3. Start the chassis host: `dotnet run --project src/Chassis.Host`
4. Configure the OIDC test client (add `chassis-loadtest` client to OpenIddict seeding)

### Run a scenario

```bash
# Steady-state (15 min — full scenario):
k6 run loadtests/k6/scenarios/steady.js \
  --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
  --tag test_run_id=local-$(date +%s)

# Spike:
k6 run loadtests/k6/scenarios/spike.js \
  --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
  --tag test_run_id=local-$(date +%s)

# Soak (4 hours — use tmux or nohup):
nohup k6 run loadtests/k6/scenarios/soak.js \
  --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
  --tag test_run_id=local-soak-$(date +%s) > /tmp/k6-soak.log 2>&1 &

# Ramp-to-break (30 min):
k6 run loadtests/k6/scenarios/ramp-to-break.js \
  --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
  --tag test_run_id=local-rtb-$(date +%s)
```

After running, open Grafana at http://localhost:3000 and load the
**Load Test Results** dashboard (`loadtest-results.json`). Select your
`test_run_id` in the template variable dropdown.

### Chaos overlays

Each chaos script contains manual setup instructions at the top of the file.
Read the comment block before running. All chaos overlays require manual
Docker commands — k6 cannot orchestrate Docker internally.

```bash
# Example: reporting-offline overlay
docker pause chassis-reporting
k6 run loadtests/k6/chaos/reporting-offline.js \
  --out experimental-prometheus-rw=http://localhost:9090/api/v1/write \
  --tag test_run_id=chaos-reporting-$(date +%s)
docker unpause chassis-reporting
```

## CI vs Nightly split

| Workflow | Trigger | Scenarios | Duration |
|---|---|---|---|
| `load-test-gate.yml` | Push to `main` | Steady-state (5m CI variant) | ~8 min total |
| `load-test-nightly.yml` | Daily 02:00 UTC | Soak (4h) + Ramp-to-break (30m) | ~5h total |

The CI gate fails the build on any k6 threshold breach. Nightly soak failures
are advisory (warn); ramp-to-break always "fails" thresholds by design (the
scenario intentionally drives beyond SLO to find the knee).

## NBomber micro-benchmarks

See `loadtests/nbomber/Dispatch.Benchmarks/README.md` for the recommended
two-run workflow to compare in-proc Mediator vs out-of-proc Bus dispatch latency.

```bash
dotnet run -c Release --project loadtests/nbomber/Dispatch.Benchmarks
```
