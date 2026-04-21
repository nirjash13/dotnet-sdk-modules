-- Reporting module — MassTransit EF Core Inbox tables.
-- Apply after 001_initial_reporting.sql.
--
-- The InboxState table records each consumed message identifier, preventing duplicate
-- processing on redelivery. This is MassTransit's first line of idempotency defence.
-- The business-level unique index on (TenantId, SourceMessageId) in transaction_projections
-- is the second line — see 001_initial_reporting.sql.
--
-- NOTE: InboxState does NOT carry a TenantId column. MT's inbox is opaque to the chassis;
-- tenant context is carried via message headers, not inbox row filtering.
-- RLS is therefore NOT applied to InboxState or OutboxMessage tables.
--
-- Apply via:
--   psql "$DATABASE_URL" -f migrations/reporting/002_mt_inbox.sql

CREATE TABLE IF NOT EXISTS reporting."InboxState" (
    "Id"                 bigserial                NOT NULL,
    "MessageId"          uuid                     NOT NULL,
    "Consumer"           text                     NOT NULL,
    "Delivered"          timestamp with time zone NULL,
    "ExpirationTime"     timestamp with time zone NULL,
    "LastSequenceNumber" bigint                   NULL,
    "ReceiveCount"       integer                  NOT NULL,
    "RowVersion"         bytea                    NULL,
    CONSTRAINT "PK_InboxState" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_InboxState_MessageId_Consumer" UNIQUE ("MessageId", "Consumer")
);

CREATE INDEX IF NOT EXISTS "IX_InboxState_Delivered"
    ON reporting."InboxState" ("Delivered");

CREATE INDEX IF NOT EXISTS "IX_InboxState_ExpirationTime"
    ON reporting."InboxState" ("ExpirationTime");

-- OutboxMessage table on the consumer side (used by MT for scheduled redelivery).
CREATE TABLE IF NOT EXISTS reporting."OutboxMessage" (
    "SequenceNumber"     bigserial                NOT NULL,
    "EnqueueTime"        timestamp with time zone NULL,
    "SentTime"           timestamp with time zone NOT NULL,
    "Headers"            text                     NULL,
    "Properties"         text                     NULL,
    "InboxMessageId"     uuid                     NULL,
    "InboxConsumer"      text                     NULL,
    "OutboxId"           uuid                     NULL,
    "MessageId"          uuid                     NOT NULL,
    "ContentType"        text                     NOT NULL,
    "MessageType"        text                     NOT NULL,
    "Body"               text                     NOT NULL,
    "ConversationId"     uuid                     NULL,
    "CorrelationId"      uuid                     NULL,
    "InitiatorId"        uuid                     NULL,
    "RequestId"          uuid                     NULL,
    "SourceAddress"      text                     NULL,
    "DestinationAddress" text                     NULL,
    "ResponseAddress"    text                     NULL,
    "FaultAddress"       text                     NULL,
    "ExpirationTime"     timestamp with time zone NULL,
    CONSTRAINT "PK_OutboxMessage_Reporting" PRIMARY KEY ("SequenceNumber")
);

CREATE TABLE IF NOT EXISTS reporting."OutboxState" (
    "OutboxId"           uuid                     NOT NULL,
    "LockId"             uuid                     NOT NULL,
    "LockToken"          bytea                    NULL,
    "LastSequenceNumber" bigint                   NULL,
    "Created"            timestamp with time zone NOT NULL,
    "Delivered"          timestamp with time zone NULL,
    "RowVersion"         bytea                    NULL,
    CONSTRAINT "PK_OutboxState_Reporting" PRIMARY KEY ("OutboxId")
);

-- EF Core migrations history entry for this migration.
INSERT INTO reporting."__ef_migrations_history" ("MigrationId", "ProductVersion")
VALUES ('20260421000101_MtInboxReporting', '10.0.6')
ON CONFLICT ("MigrationId") DO NOTHING;
