-- RLS policy template for the Foo module.
-- Replace <table_name> with actual table names after running EF Core migrations.

-- ALTER TABLE foo.<table_name> ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE foo.<table_name> FORCE ROW LEVEL SECURITY;

-- DROP POLICY IF EXISTS tenant_isolation ON foo.<table_name>;
-- CREATE POLICY tenant_isolation
--     ON foo.<table_name>
--     USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
