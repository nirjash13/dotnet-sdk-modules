-- RLS policy template for tenant-scoped tables.
-- Replace {schema} and {table} with the actual schema and table names before applying.
--
-- Apply example (bash):
--   export SCHEMA=iam TABLE=users
--   envsubst < migrations/_template/rls-policy.sql | psql "$DATABASE_URL"
--
-- Or with sed:
--   sed -e 's/{schema}/iam/g' -e 's/{table}/users/g' \
--       migrations/_template/rls-policy.sql | psql "$DATABASE_URL"

-- Step 1: Enable RLS on the table.
ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY;

-- Step 2: FORCE RLS so even the table owner (superuser-equivalent role) sees filtered rows.
-- This closes the "owner bypass" gap and is required for true defense-in-depth.
ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY;

-- Step 3: Create the tenant isolation policy.
-- USING clause: filters SELECTs and the target of UPDATE/DELETE.
-- WITH CHECK clause: filters INSERTs and the new row of UPDATE.
-- current_setting('app.tenant_id', true) — the second arg (true = missing-ok) returns NULL
-- when the setting is absent, which causes the cast to UUID to fail-safe to false (no rows).
CREATE POLICY tenant_isolation ON {schema}.{table}
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);

-- Reverse (drop policy) — run this before re-creating or when decommissioning a table:
-- DROP POLICY IF EXISTS tenant_isolation ON {schema}.{table};
-- ALTER TABLE {schema}.{table} DISABLE ROW LEVEL SECURITY;
-- ALTER TABLE {schema}.{table} NO FORCE ROW LEVEL SECURITY;
