-- Reporting module — initial schema, transaction_projections table, RLS policies.
-- Apply via:
--   psql "$DATABASE_URL" -f migrations/reporting/001_initial_reporting.sql
--
-- This script is idempotent (IF NOT EXISTS guards where possible).

-- ── Schema ────────────────────────────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS reporting;

-- ── transaction_projections ───────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS reporting.transaction_projections (
    "Id"              uuid                     NOT NULL,
    "TenantId"        uuid                     NOT NULL,
    "SourceMessageId" uuid                     NOT NULL,
    "AccountId"       uuid                     NOT NULL,
    "Amount"          numeric(19,4)            NOT NULL,
    "Currency"        char(3)                  NOT NULL,
    "OccurredAt"      timestamp with time zone NOT NULL,
    CONSTRAINT "PK_transaction_projections" PRIMARY KEY ("Id")
);

-- Business-level idempotency guard: prevents duplicate projection rows for the
-- same (tenant, source message) pair. This is the second line of defence after
-- MassTransit InboxState prevents re-delivery.
CREATE UNIQUE INDEX IF NOT EXISTS "ix_transaction_projections_tenant_source_message"
    ON reporting.transaction_projections ("TenantId", "SourceMessageId");

-- Composite index for tenant-scoped queries.
CREATE INDEX IF NOT EXISTS "IX_transaction_projections_TenantId_Id"
    ON reporting.transaction_projections ("TenantId", "Id");

-- ── RLS: transaction_projections ──────────────────────────────────────────────

ALTER TABLE reporting.transaction_projections ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.transaction_projections FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE schemaname = 'reporting'
          AND tablename  = 'transaction_projections'
          AND policyname = 'tenant_isolation'
    ) THEN
        CREATE POLICY tenant_isolation ON reporting.transaction_projections
            USING ("TenantId" = current_setting('app.tenant_id', true)::uuid)
            WITH CHECK ("TenantId" = current_setting('app.tenant_id', true)::uuid);
    END IF;
END;
$$;

-- ── EF Core migrations history ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS reporting."__ef_migrations_history" (
    "MigrationId"    character varying(150) NOT NULL,
    "ProductVersion" character varying(32)  NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);

INSERT INTO reporting."__ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES ('20260421000100_InitialReporting', '10.0.6')
ON CONFLICT ("MigrationId") DO NOTHING;
