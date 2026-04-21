using System;
using Chassis.SharedKernel.Tenancy;

namespace Reporting.Application.Persistence;

/// <summary>
/// Read-side projection row persisted by <see cref="Consumers.LedgerTransactionPostedConsumer"/>.
/// Each row represents one processed <c>LedgerTransactionPosted</c> event.
/// The unique index on <c>(TenantId, SourceMessageId)</c> provides the business-level idempotency guard.
/// </summary>
public sealed class TransactionProjection : ITenantScoped
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant that owns this projection row.</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Gets or sets the source message identifier from the integration event.
    /// Used as the business-level idempotency discriminator alongside <see cref="TenantId"/>.
    /// </summary>
    public Guid SourceMessageId { get; set; }

    /// <summary>Gets or sets the originating ledger account identifier.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Gets or sets the monetary amount of the transaction.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the 3-character ISO 4217 currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the transaction occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}
