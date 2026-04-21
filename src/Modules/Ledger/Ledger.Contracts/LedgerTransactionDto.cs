using System;

namespace Ledger.Contracts;

/// <summary>
/// Response DTO for a ledger transaction (posting).
/// </summary>
public sealed class LedgerTransactionDto
{
    /// <summary>Initializes all required fields.</summary>
    public LedgerTransactionDto(
        Guid id,
        Guid accountId,
        decimal amount,
        string currency,
        string? memo,
        DateTimeOffset occurredAt,
        Guid? idempotencyKey)
    {
        Id = id;
        AccountId = accountId;
        Amount = amount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        Memo = memo;
        OccurredAt = occurredAt;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Gets the posting identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the account the transaction belongs to.</summary>
    public Guid AccountId { get; }

    /// <summary>Gets the monetary amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Gets the optional memo.</summary>
    public string? Memo { get; }

    /// <summary>Gets the UTC timestamp when the transaction was posted.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Gets the client-supplied idempotency key, if any.</summary>
    public Guid? IdempotencyKey { get; }
}
