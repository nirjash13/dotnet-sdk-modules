using System;
using Chassis.SharedKernel.Abstractions;

namespace Ledger.Contracts;

/// <summary>
/// Integration event published when a transaction is successfully posted to a ledger account.
/// Consumed by downstream modules (e.g. Reporting) that need to react to ledger activity.
/// </summary>
/// <remarks>
/// Uses a class with read-only properties (not a record with positional parameters) because
/// this type multi-targets <c>netstandard2.0</c> which does not have
/// <c>System.Runtime.CompilerServices.IsExternalInit</c>. See CHANGELOG_AI.md Phase 0 design note.
/// </remarks>
public sealed class LedgerTransactionPosted : IIntegrationEvent
{
    /// <summary>Initializes all required fields.</summary>
    public LedgerTransactionPosted(
        Guid tenantId,
        Guid transactionId,
        Guid accountId,
        decimal amount,
        string currency,
        string? memo,
        DateTimeOffset occurredAt)
    {
        TenantId = tenantId;
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = amount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        Memo = memo;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the tenant that owns the account.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the unique identifier of the posting (Posting.Id).</summary>
    public Guid TransactionId { get; }

    /// <summary>Gets the account the transaction was posted to.</summary>
    public Guid AccountId { get; }

    /// <summary>Gets the monetary amount of the transaction.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Gets the optional memo for this transaction.</summary>
    public string? Memo { get; }

    /// <summary>Gets the UTC timestamp when the transaction was posted.</summary>
    public DateTimeOffset OccurredAt { get; }
}
