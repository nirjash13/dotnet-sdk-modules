CREATE TABLE IF NOT EXISTS public."InboxState" (
    "Id" bigserial NOT NULL,
    "MessageId" uuid NOT NULL,
    "Consumer" text NOT NULL,
    "Delivered" timestamp with time zone NULL,
    "ExpirationTime" timestamp with time zone NULL,
    "LastSequenceNumber" bigint NULL,
    "ReceiveCount" integer NOT NULL,
    "RowVersion" bytea NULL,
    CONSTRAINT "PK_InboxState" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_InboxState_MessageId_Consumer" UNIQUE ("MessageId", "Consumer")
);

CREATE INDEX IF NOT EXISTS "IX_InboxState_Delivered"
    ON public."InboxState" ("Delivered");

CREATE INDEX IF NOT EXISTS "IX_InboxState_ExpirationTime"
    ON public."InboxState" ("ExpirationTime");

CREATE SCHEMA IF NOT EXISTS transport;

CREATE TABLE IF NOT EXISTS transport.outbox_message (
    enqueue_time timestamp with time zone NULL,
    delivered timestamp with time zone NULL
);
