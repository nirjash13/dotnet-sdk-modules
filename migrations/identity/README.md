# Identity Module — EF Core Migrations

Migrations for the Identity bounded context live here after generation.

## Generating the initial migration

```bash
dotnet ef migrations add InitialIdentity \
  --project src/Modules/Identity/Identity.Infrastructure \
  --startup-project src/Chassis.Host \
  --output-dir Migrations \
  --context IdentityDbContext
```

## Applying migrations

```bash
dotnet ef database update \
  --project src/Modules/Identity/Identity.Infrastructure \
  --startup-project src/Chassis.Host \
  --context IdentityDbContext
```

## Listing migrations

```bash
dotnet ef migrations list \
  --project src/Modules/Identity/Identity.Infrastructure \
  --startup-project src/Chassis.Host \
  --context IdentityDbContext
```

## Removing the last migration (if unapplied)

```bash
dotnet ef migrations remove \
  --project src/Modules/Identity/Identity.Infrastructure \
  --startup-project src/Chassis.Host \
  --context IdentityDbContext
```

## Notes

- All migrations must be reviewed before applying to staging or production.
- Migration `002_TenantIdColumns` (Phase 2 follow-up) adds `tenant_id` columns to OpenIddict
  tables and installs Postgres RLS policies per `migrations/_template/rls-policy.sql`.
- Never use `EnsureCreated()` in production — always use `Migrate()`.
