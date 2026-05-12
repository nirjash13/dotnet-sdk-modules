# SaasBuilder.Sample

A multi-tenant SaaS API scaffolded from the **SaaS Builder SDK** `saas-api` template.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for local backing services)

## Quick start

### 1. Start backing services

```bash
docker compose up -d
```

This starts Postgres (`:5432`), RabbitMQ (`:5672`, management UI `:15672`), the OTel Collector (`:4317`/`:4318`), and Mailhog (SMTP `:1025`, web UI `:8025`).

### 2. Apply database migrations

```bash
dotnet ef database update
```

### 3. Run the API

```bash
dotnet run
```

### 4. Verify

```bash
curl http://localhost:5000/health
```

Expected response: `{"status":"Healthy"}`

## Configuration

| Key | Default | Purpose |
|-----|---------|---------|
| `ConnectionStrings:DefaultConnection` | `Host=localhost;...` | Postgres connection |
| `Auth:Issuer` | `http://localhost:5000` (dev) | JWT issuer |
| `Auth:Audience` | `SaasBuilder.Sample` | JWT audience |
| `Otel:Endpoint` | `http://localhost:4317` | OTLP gRPC endpoint |
| `RabbitMq:Host` | `localhost` | RabbitMQ host (bus transport only) |

The `appsettings.Development.json` ships with an **empty** `Password=` placeholder — never put a real password in that file.

Supply credentials via **dotnet user-secrets** (local dev):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=SaasBuilder.Sample;Username=saas;Password=<your-password>"
```

Or via **environment variables** (CI/staging/production):

```bash
export ConnectionStrings__DefaultConnection="Host=...;Password=<your-password>"
```

For production, override all secrets via environment variables or a secrets manager. Never commit secrets to source control.

## Adding modules

Register module consumers in `Program.cs` via `opts.Transport.WithMediatorConsumers(cfg => { ... })`. See the [SaasBuilder SDK docs](https://docs.saasbuilder.dev) for module authoring guides.

## Docs

Full documentation: [https://docs.saasbuilder.dev](https://docs.saasbuilder.dev) *(placeholder — site ships with Phase 9)*
