# Ledger Module Migrations

## Option A — Apply via psql (recommended for production)

```bash
psql "$DATABASE_URL" -f migrations/ledger/001_initial_ledger.sql
```

The script is idempotent: it uses `CREATE TABLE IF NOT EXISTS`, `DO $$...$$` guards for RLS
policies, and `CREATE INDEX IF NOT EXISTS`.

## Option B — Apply via EF Core (development)

```bash
dotnet ef database update \
  --project src/Modules/Ledger/Ledger.Infrastructure \
  --startup-project src/Chassis.Host \
  --context LedgerDbContext
```

## Option C — Generate a new migration

After changing domain entities or configurations:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Modules/Ledger/Ledger.Infrastructure \
  --startup-project src/Chassis.Host \
  --context LedgerDbContext \
  --output-dir Persistence/Migrations
```

Review the generated migration before applying to staging or production.
Migrations that drop columns or tables require a two-phase deployment.

## RLS policy notes

Both `ledger.accounts` and `ledger.postings` have:

- `ENABLE ROW LEVEL SECURITY` — activates RLS filtering for non-superuser roles.
- `FORCE ROW LEVEL SECURITY` — applies filtering even to the table owner, closing the
  owner-bypass gap.
- `CREATE POLICY tenant_isolation` — uses `current_setting('app.tenant_id', true)::uuid`
  which is set by `TenantCommandInterceptor` before every EF Core command.

The second argument `true` (missing-ok) causes `current_setting` to return `NULL` when the
setting is absent, which causes the UUID cast to fail-safe — no rows are returned rather
than all rows being exposed.
