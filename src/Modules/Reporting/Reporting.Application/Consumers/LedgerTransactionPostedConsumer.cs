using System;
using System.Threading.Tasks;
using Ledger.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using Reporting.Application.Abstractions;
using Reporting.Application.Persistence;

namespace Reporting.Application.Consumers;

/// <summary>
/// MassTransit consumer that projects <see cref="LedgerTransactionPosted"/> integration events
/// into the Reporting read-side <see cref="TransactionProjection"/> table.
/// </summary>
/// <remarks>
/// <para>
/// Idempotency is guaranteed at two levels:
/// <list type="bullet">
///   <item>MassTransit <c>InboxState</c> table — prevents double-delivery of the same MT message.</item>
///   <item>Business-level unique index on <c>(TenantId, SourceMessageId)</c> — prevents duplicate
///       projection rows even if the inbox check is bypassed (e.g. direct publish in tests).</item>
/// </list>
/// </para>
/// <para>
/// This consumer has <b>no import of any RabbitMQ-specific type</b>. It runs identically
/// under the <c>inproc</c> (MassTransit Mediator) and <c>bus</c> (RabbitMQ) transports.
/// Transport selection is controlled by <c>Dispatch:Transport</c> configuration in the Host.
/// </para>
/// </remarks>
public sealed class LedgerTransactionPostedConsumer(
    IReportingDbContext db,
    ILogger<LedgerTransactionPostedConsumer> logger)
    : IConsumer<LedgerTransactionPosted>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<LedgerTransactionPosted> context)
    {
        LedgerTransactionPosted message = context.Message;

        // Use the MassTransit MessageId as the source message identifier for idempotency.
        // Falls back to the TransactionId if the message has no assigned MessageId.
        // context.MessageId is already Guid? — no conversion needed.
        Guid sourceMessageId = context.MessageId ?? message.TransactionId;

        logger.LogDebug(
            "LedgerTransactionPostedConsumer: processing message {MessageId} for tenant {TenantId}",
            sourceMessageId,
            message.TenantId);

        // Short-circuit if the projection row already exists (business-level idempotency check).
        bool exists = await db.ExistsAsync(message.TenantId, sourceMessageId, context.CancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            logger.LogInformation(
                "LedgerTransactionPostedConsumer: duplicate message {MessageId} for tenant {TenantId} — skipping.",
                sourceMessageId,
                message.TenantId);
            return;
        }

        TransactionProjection projection = new TransactionProjection
        {
            Id = NewComb(),
            TenantId = message.TenantId,
            SourceMessageId = sourceMessageId,
            AccountId = message.AccountId,
            Amount = message.Amount,
            Currency = message.Currency,
            OccurredAt = message.OccurredAt,
        };

        // InsertIfNotExistsAsync is idempotent: it ignores conflicts on the unique index.
        // This handles the race between the ExistsAsync check above and the actual insert.
        await db.InsertIfNotExistsAsync(projection, context.CancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "LedgerTransactionPostedConsumer: projected transaction {TransactionId} for tenant {TenantId}.",
            message.TransactionId,
            message.TenantId);
    }

    /// <summary>
    /// Generates a sequential GUID (COMB) for clustering-friendly inserts.
    /// </summary>
    private static Guid NewComb() => NewId.NextSequentialGuid();
}
