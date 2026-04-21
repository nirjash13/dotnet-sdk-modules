-- Ledger module — initial schema, RLS policies.
-- Apply via:
--   psql "$DATABASE_URL" -f migrations/ledger/001_initial_ledger.sql
-- Or via EF Core:
--   dotnet ef database update --project src/Modules/Ledger/Ledger.Infrastructure --startup-project src/Chassis.Host
--
-- This script is idempotent (IF NOT EXISTS guards where possible).

-- ── Schema ────────────────────────────────────────────────────────────────────

CREATE SCHEMA IF NOT EXISTS ledger;

-- ── accounts ──────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS ledger.accounts (
    "Id"        uuid                     NOT NULL,
    "TenantId"  uuid                     NOT NULL,
    "Name"      character varying(200)   NOT NULL,
    "Currency"  char(3)                  NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_accounts" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_accounts_TenantId_Id"
    ON ledger.accounts ("TenantId", "Id");

-- RLS: accounts
ALTER TABLE ledger.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE ledger.accounts FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE schemaname = 'ledger'
          AND tablename  = 'accounts'
          AND policyname = 'tenant_isolation'
    ) THEN
        CREATE POLICY tenant_isolation ON ledger.accounts
            USING ("TenantId" = current_setting('app.tenant_id', true)::uuid)
            WITH CHECK ("TenantId" = current_setting('app.tenant_id', true)::uuid);
    END IF;
END;
$$;

-- ── postings ──────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS ledger.postings (
    "Id"              uuid                     NOT NULL,
    "TenantId"        uuid                     NOT NULL,
    "AccountId"       uuid                     NOT NULL,
    amount            numeric(19,4)            NOT NULL,
    currency          char(3)                  NOT NULL,
    "OccurredAt"      timestamp with time zone NOT NULL,
    "Memo"            character varying(500)   NULL,
    "IdempotencyKey"  uuid                     NULL,
    CONSTRAINT "PK_postings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_postings_accounts_AccountId"
        FOREIGN KEY ("AccountId")
        REFERENCES ledger.accounts ("Id")
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_postings_AccountId"
    ON ledger.postings ("AccountId");

-- Partial unique index for idempotency deduplication.
-- Only active when IdempotencyKey is not null.
CREATE UNIQUE INDEX IF NOT EXISTS "ix_postings_tenant_idempotency_key"
    ON ledger.postings ("TenantId", "IdempotencyKey")
    WHERE "IdempotencyKey" IS NOT NULL;

-- RLS: postings
ALTER TABLE ledger.postings ENABLE ROW LEVEL SECURITY;
ALTER TABLE ledger.postings FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE schemaname = 'ledger'
          AND tablename  = 'postings'
          AND policyname = 'tenant_isolation'
    ) THEN
        CREATE POLICY tenant_isolation ON ledger.postings
            USING ("TenantId" = current_setting('app.tenant_id', true)::uuid)
            WITH CHECK ("TenantId" = current_setting('app.tenant_id', true)::uuid);
    END IF;
END;
$$;

-- ── EF Core migrations history ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS ledger."__ef_migrations_history" (
    "MigrationId"    character varying(150) NOT NULL,
    "ProductVersion" character varying(32)  NOT NULL,
    CONSTRAINT "PK___ef_migrations_history" PRIMARY KEY ("MigrationId")
);
