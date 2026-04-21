using System;

namespace Reporting.Contracts;

/// <summary>
/// Read-side view model for a transaction projection in the Reporting module.
/// Populated from <c>LedgerTransactionPosted</c> integration events via the consumer.
/// </summary>
public sealed class TransactionProjectionDto
{
    /// <summary>Initializes all required fields.</summary>
    public TransactionProjectionDto(
        Guid id,
        Guid tenantId,
        Guid sourceMessageId,
        Guid accountId,
        decimal amount,
        string currency,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        SourceMessageId = sourceMessageId;
        AccountId = accountId;
        Amount = amount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the projection row identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the tenant that owns this projection row.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the source message identifier used for idempotency deduplication.</summary>
    public Guid SourceMessageId { get; }

    /// <summary>Gets the ledger account identifier.</summary>
    public Guid AccountId { get; }

    /// <summary>Gets the monetary amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Gets the UTC timestamp when the transaction occurred.</summary>
    public DateTimeOffset OccurredAt { get; }
}
