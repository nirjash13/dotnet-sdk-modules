# Registration Schema — Migration Guide

## Overview

Migrations in this folder manage the `registration` PostgreSQL schema.
They are intentionally **separate from EF Core auto-migrations** so a DBA
can review and apply them during deployment without running application code.

## Files

| File | Purpose |
|---|---|
| `001_initial_registration.sql` | Creates `registration` schema, `registration_saga_state` table, and `__ef_migrations_history` table |

## Applying Migrations

### First-time setup (local or staging)

```bash
psql -h <host> -U <user> -d <database> -f migrations/registration/001_initial_registration.sql
```

All scripts are **idempotent** (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`).
Running a script twice is safe.

### Production deployment

1. Connect to the target database with a role that has `CREATE SCHEMA` and `CREATE TABLE` privileges.
2. Run each script in numeric order:

```bash
psql "$DATABASE_URL" -f migrations/registration/001_initial_registration.sql
```

3. Verify the table exists:

```sql
SELECT table_name FROM information_schema.tables
WHERE table_schema = 'registration';
-- Expected: registration_saga_state, __ef_migrations_history
```

### EF Core tooling (dotnet-ef)

The `registration.__ef_migrations_history` table is created by the SQL migration above
so that `dotnet ef migrations list` and `dotnet ef database update` can track applied
EF Core migrations against the `RegistrationDbContext`.

```bash
dotnet ef database update \
  --project src/Modules/Registration/Registration.Infrastructure \
  --startup-project src/Chassis.Host
```

## Row-Level Security

`registration.registration_saga_state` does **not** have RLS enabled.
See the comment block at the top of `001_initial_registration.sql` for the full rationale.

## Adding New Migrations

Name files with a zero-padded sequence number and a descriptive slug:

```
002_add_registration_saga_retry_count.sql
```

All scripts must be idempotent. Use `IF NOT EXISTS` / `IF EXISTS` guards throughout.
