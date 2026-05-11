using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Contracts;

/// <summary>
/// Integration event published when Stripe reports <c>invoice.payment_failed</c>.
///
/// Contract consumed by:
/// - <c>SaasBuilder.Modules.Notifications</c> — sends dunning email to the tenant owner.
///   Expected handler: subscribe with MassTransit consumer <c>InvoicePaymentFailedConsumer</c>.
///   Dunning emails improve payment recovery rate by 20-40%.
///
/// Schema stability: this is a public contract. Fields added must be backward-compatible (non-breaking).
/// Remove or rename fields only with a major version bump and migration guide.
/// </summary>
public sealed class InvoicePaymentFailedIntegrationEvent : IIntegrationEvent
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public InvoicePaymentFailedIntegrationEvent()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public InvoicePaymentFailedIntegrationEvent(
        Guid tenantId,
        string providerInvoiceId,
        long amountDueCents,
        string currency,
        int attemptCount,
        DateTimeOffset failedAt,
        string? nextRetryAt)
    {
        TenantId = tenantId;
        ProviderInvoiceId = providerInvoiceId;
        AmountDueCents = amountDueCents;
        Currency = currency;
        AttemptCount = attemptCount;
        FailedAt = failedAt;
        NextRetryAt = nextRetryAt;
    }

    /// <summary>Gets the tenant whose invoice payment failed.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets the Stripe invoice identifier (inv_xxx).</summary>
    public string ProviderInvoiceId { get; set; } = string.Empty;

    /// <summary>Gets the amount due in the smallest currency unit (e.g., cents).</summary>
    public long AmountDueCents { get; set; }

    /// <summary>Gets the 3-character ISO 4217 currency code (e.g., "usd").</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Gets the number of failed payment attempts (1 on first failure).</summary>
    public int AttemptCount { get; set; }

    /// <summary>Gets the UTC timestamp when the payment failure occurred.</summary>
    public DateTimeOffset FailedAt { get; set; }

    /// <summary>Gets the ISO-8601 string of the next retry timestamp, or null if no retry is scheduled.</summary>
    public string? NextRetryAt { get; set; }
}
