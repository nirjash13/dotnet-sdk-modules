<!-- written-by: Claude Code (Phase 7 implementation) -->
# Local Observability Stack

Starts OTel Collector, Prometheus, Loki, Tempo, and Grafana for local development.

## Start

```bash
docker compose up -d
```

## Ports

| Service        | Port  | Purpose                             |
|----------------|-------|-------------------------------------|
| Grafana        | 3000  | Dashboards — admin / admin          |
| Prometheus     | 9090  | Metrics query + remote-write        |
| Loki           | 3100  | Log query                           |
| Tempo          | 3200  | Trace query                         |
| OTel Collector | 4317  | OTLP gRPC — point your .NET host here |
| OTel Collector | 4318  | OTLP HTTP                           |

## Configure the .NET host

Set in `appsettings.Development.json` or environment:

```json
{ "Otel": { "Endpoint": "http://localhost:4317" } }
```

Or via environment variable (overrides config):

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

## Stop

```bash
docker compose down
```

To also remove persisted data volumes:

```bash
docker compose down -v
```
