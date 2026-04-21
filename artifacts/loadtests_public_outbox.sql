CREATE TABLE IF NOT EXISTS public."OutboxState" (
    "OutboxId" uuid NOT NULL,
    "LockId" uuid NOT NULL,
    "LockToken" bytea NULL,
    "LastSequenceNumber" bigint NULL,
    "Created" timestamp with time zone NOT NULL,
    "Delivered" timestamp with time zone NULL,
    "RowVersion" bytea NULL,
    CONSTRAINT "PK_OutboxState" PRIMARY KEY ("OutboxId")
);

CREATE TABLE IF NOT EXISTS public."OutboxMessage" (
    "SequenceNumber" bigserial NOT NULL,
    "EnqueueTime" timestamp with time zone NULL,
    "SentTime" timestamp with time zone NOT NULL,
    "Headers" text NULL,
    "Properties" text NULL,
    "InboxMessageId" uuid NULL,
    "InboxConsumer" text NULL,
    "OutboxId" uuid NULL,
    "MessageId" uuid NOT NULL,
    "ContentType" text NOT NULL,
    "MessageType" text NOT NULL,
    "Body" text NOT NULL,
    "ConversationId" uuid NULL,
    "CorrelationId" uuid NULL,
    "InitiatorId" uuid NULL,
    "RequestId" uuid NULL,
    "SourceAddress" text NULL,
    "DestinationAddress" text NULL,
    "ResponseAddress" text NULL,
    "FaultAddress" text NULL,
    "ExpirationTime" timestamp with time zone NULL,
    CONSTRAINT "PK_OutboxMessage" PRIMARY KEY ("SequenceNumber")
);

CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_EnqueueTime"
    ON public."OutboxMessage" ("EnqueueTime");

CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_ExpirationTime"
    ON public."OutboxMessage" ("ExpirationTime");

CREATE INDEX IF NOT EXISTS "IX_OutboxMessage_OutboxId_SequenceNumber"
    ON public."OutboxMessage" ("OutboxId", "SequenceNumber");
