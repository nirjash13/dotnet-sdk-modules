using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ledger.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds MassTransit EF Core Outbox tables to the ledger schema.
/// These tables are managed by MassTransit.EntityFrameworkCore and support
/// at-least-once delivery with transactional outbox semantics.
/// </summary>
/// <remarks>
/// The outbox tables do not carry a TenantId column.
/// Tenant context is propagated via message headers, not outbox row filtering.
/// RLS is therefore not applied to these tables.
/// </remarks>
public partial class MtOutboxLedger : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxState",
            schema: "ledger",
            columns: table => new
            {
                OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                LockId = table.Column<Guid>(type: "uuid", nullable: false),
                LockToken = table.Column<byte[]>(type: "bytea", nullable: true),
                LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true),
                Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Delivered = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RowVersion = table.Column<byte[]>(type: "bytea", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
            });

        migrationBuilder.CreateTable(
            name: "OutboxMessage",
            schema: "ledger",
            columns: table => new
            {
                SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                EnqueueTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                SentTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Headers = table.Column<string>(type: "text", nullable: true),
                Properties = table.Column<string>(type: "text", nullable: true),
                InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                InboxConsumer = table.Column<string>(type: "text", nullable: true),
                OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                ContentType = table.Column<string>(type: "text", nullable: false),
                MessageType = table.Column<string>(type: "text", nullable: false),
                Body = table.Column<string>(type: "text", nullable: false),
                ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                SourceAddress = table.Column<string>(type: "text", nullable: true),
                DestinationAddress = table.Column<string>(type: "text", nullable: true),
                ResponseAddress = table.Column<string>(type: "text", nullable: true),
                FaultAddress = table.Column<string>(type: "text", nullable: true),
                ExpirationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_EnqueueTime",
            table: "OutboxMessage",
            schema: "ledger",
            column: "EnqueueTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_ExpirationTime",
            table: "OutboxMessage",
            schema: "ledger",
            column: "ExpirationTime");

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessage_OutboxId_SequenceNumber",
            table: "OutboxMessage",
            schema: "ledger",
            columns: new[] { "OutboxId", "SequenceNumber" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxMessage", schema: "ledger");
        migrationBuilder.DropTable(name: "OutboxState", schema: "ledger");
    }
}
