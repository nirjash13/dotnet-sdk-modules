# Dispatch.Benchmarks — NBomber Dispatcher Micro-Benchmarks

Compares in-proc Mediator dispatch vs out-of-proc Bus dispatch latency for the
`PostTransactionCommand` handler. Both scenarios target the running chassis host
via HTTP and report mean + p95 in NBomber's stdout format.

## Prerequisites

- .NET 10 SDK installed (`dotnet --version`)
- Chassis host running (start with `dotnet run --project src/Chassis.Host` from the repo root)
- For out-of-proc Bus: RabbitMQ running (`docker compose up -d rabbitmq` from `deploy/`)
- (Optional) A valid bearer token in `BEARER_TOKEN` env var for authenticated endpoints

## Run

```bash
# Release build for accurate latency numbers (avoids JIT overhead).
dotnet run -c Release --project loadtests/nbomber/Dispatch.Benchmarks
```

## Recommended two-run workflow

To compare dispatcher modes accurately, run the chassis host twice — once per mode —
and compare the NBomber reports:

### Run 1: In-process Mediator (Transport=InMemory)

```bash
# Terminal 1 — start chassis in in-proc mode:
Transport__Mode=InMemory dotnet run -c Release --project src/Chassis.Host

# Terminal 2 — run benchmarks:
dotnet run -c Release --project loadtests/nbomber/Dispatch.Benchmarks
```

### Run 2: Out-of-process Bus (Transport=Bus)

```bash
# Start RabbitMQ:
docker compose -f deploy/docker-compose.yml up -d rabbitmq

# Terminal 1 — start chassis in bus mode:
Transport__Mode=Bus dotnet run -c Release --project src/Chassis.Host

# Terminal 2 — run benchmarks:
dotnet run -c Release --project loadtests/nbomber/Dispatch.Benchmarks
```

Compare the `out_of_proc_bus / post_transaction_bus` p95 from Run 2 with
`in_proc_mediator / post_transaction_mediator` p95 from Run 1. The difference
is the overhead of RabbitMQ serialization + publish + consumer processing.

## Env vars

| Variable | Default | Description |
|---|---|---|
| `BASE_URL` | `http://localhost:5000` | Chassis host base URL |
| `TENANT_ID` | `00000000-0000-0000-0000-000000000001` | Tenant UUID for X-Tenant-Id header |
| `BEARER_TOKEN` | `` (empty) | JWT bearer token; leave empty if endpoint is unauthenticated |

## Adding to Chassis.sln (on demand)

This project is excluded from `Chassis.sln` by default to keep the main solution
build fast. To add it temporarily:

```bash
dotnet sln ../../Chassis.sln add loadtests/nbomber/Dispatch.Benchmarks/Dispatch.Benchmarks.csproj
```

Remove it again after benchmarking:

```bash
dotnet sln ../../Chassis.sln remove loadtests/nbomber/Dispatch.Benchmarks/Dispatch.Benchmarks.csproj
```

## Package versions

NBomber 6.0.0 is pinned directly in the `.csproj` (not in `Directory.Packages.props`)
because this project is outside the main solution and must not affect the CPM graph.
NBomber is a dev-only dependency with no production impact.
