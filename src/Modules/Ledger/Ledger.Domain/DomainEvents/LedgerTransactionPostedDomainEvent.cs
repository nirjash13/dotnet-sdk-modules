using System;
using Chassis.SharedKernel.Abstractions;

namespace Ledger.Domain.DomainEvents;

/// <summary>
/// Raised by <c>Account.Post()</c> after a <c>Posting</c> is successfully applied.
/// Dispatched in-process; consumed by the application layer to publish the corresponding
/// <c>LedgerTransactionPosted</c> integration event after the unit of work commits.
/// </summary>
public sealed class LedgerTransactionPostedDomainEvent : IDomainEvent
{
    /// <summary>Initializes the domain event with all required identifiers.</summary>
    public LedgerTransactionPostedDomainEvent(
        Guid tenantId,
        Guid accountId,
        Guid postingId,
        decimal amount,
        string currency,
        string? memo,
        DateTimeOffset occurredAt)
    {
        TenantId = tenantId;
        AccountId = accountId;
        PostingId = postingId;
        Amount = amount;
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        Memo = memo;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the tenant that owns the account.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the account the transaction was posted to.</summary>
    public Guid AccountId { get; }

    /// <summary>Gets the posting identifier.</summary>
    public Guid PostingId { get; }

    /// <summary>Gets the monetary amount.</summary>
    public decimal Amount { get; }

    /// <summary>Gets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; }

    /// <summary>Gets the optional memo.</summary>
    public string? Memo { get; }

    /// <summary>Gets the UTC timestamp when the posting occurred.</summary>
    public DateTimeOffset OccurredAt { get; }
}
