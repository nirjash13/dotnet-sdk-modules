using System;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// Records a processed webhook event for idempotency deduplication.
/// The unique constraint on <see cref="IdempotencyKey"/> prevents double-processing.
/// </summary>
public sealed class WebhookEvent
{
    /// <summary>Gets the database row identifier.</summary>
    public long RowId { get; private set; }

    /// <summary>Gets the provider-supplied idempotency key (e.g., Stripe event ID).</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Gets the provider event type (e.g., "customer.subscription.updated").</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Gets the provider name that delivered the event.</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when this event was received and processed.</summary>
    public DateTimeOffset ProcessedAt { get; private set; }

    /// <summary>Creates a new webhook event record.</summary>
    public static WebhookEvent Record(string idempotencyKey, string eventType, string providerName) =>
        new WebhookEvent
        {
            IdempotencyKey = idempotencyKey,
            EventType = eventType,
            ProviderName = providerName,
            ProcessedAt = DateTimeOffset.UtcNow,
        };
}
