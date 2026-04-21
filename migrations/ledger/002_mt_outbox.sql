-- Ledger module — MassTransit EF Core Outbox tables.
-- Apply after 001_initial_ledger.sql.
--
-- These tables are managed by MassTransit.EntityFrameworkCore and are created
-- automatically by EF Core migrations when the bus transport is active.
-- This SQL file is provided for reference and manual DBA review.
--
-- NOTE: MT outbox tables do NOT carry a TenantId column.
-- Tenant context is propagated via message headers (set by TenantPropagationSendFilter),
-- not by row-level filtering on outbox rows. RLS is therefore NOT applied here.
-- The chassis TenantCommandInterceptor issues SET LOCAL app.tenant_id before every command,
-- but MT's delivery service runs outside a per-tenant scope; it reads all pending outbox rows
-- for the current node and forwards them to the broker regardless of tenant.
--
-- Apply via:
--   psql "$DATABASE_URL" -f migrations/ledger/002_mt_outbox.sql

CREATE TABLE IF NOT EXISTS ledger."OutboxState" (
    "OutboxId"       uuid                     NOT NULL,
    "LockId"         uuid                     NOT NULL,
    "LockToken"      bytea                    NULL,
    "LastSequenceNumber" bigint               NULL,
    "Created"        timestamp with time zone NOT NULL,
    "Delivered"      timestamp with time zone NULL,
    "RowVersion"     bytea                    NULL,
    CONSTRAINT "PK_OutboxState" PRIMARY KEY ("OutboxId")
);

CREATE TABLE IF NOT EXISTS ledger."OutboxMessage" (
    "SequenceNumber"    bigserial                NOT NULL,
    "EnqueueTime"       timestamp with time zone NULL,
    "SentTime"          timestamp with time zone NOT NULL,
    "Headers"           text                     NULL,
    "Properties"        text                     NULL,
    "InboxMessageId"    uuid                     NULL,
    "InboxConsumer"     text                     NULL,
    "OutboxId"          uuid                     NULL,
    "MessageId"         uuid                     NOT NULL,
    "ContentType"       text                     NOT NULL,
    "MessageType"       text                     NOT NULL,
    "Body"              text                     NOT NULL,
    "ConversationId"    uuid                     NULL,
    "CorrelationId"     uuid                     NULL,
    "InitiatorId"       uuid                     NULL,
    "RequestId"         uuid                     NULL,
    "SourceAddress"     text                     NULL,
    "DestinationAddress" text                    NULL,
    "ResponseAddress"   text                     NULL,
    "FaultAddress"      text                     NULL,
    "ExpirationTime"    timestamp with time zone NULL,
    CONSTRAINT "PK_OutboxMessage" PRIMARY KEY ("SequenceNumber")
);

CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_EnqueueTime"
    ON ledger."OutboxMessage" ("EnqueueTime");

CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_ExpirationTime"
    ON ledger."OutboxMessage" ("ExpirationTime");

CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_OutboxId_SequenceNumber"
    ON ledger."OutboxMessage" ("OutboxId", "SequenceNumber");

-- EF Core migrations history entry for this migration.
INSERT INTO ledger."__ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES ('20260421000001_MtOutboxLedger', '10.0.6')
ON CONFLICT ("MigrationId") DO NOTHING;
