using System.Threading;
using System.Threading.Tasks;

namespace Billing.Application.Abstractions;

/// <summary>
/// Repository for idempotency deduplication of inbound webhook events.
/// </summary>
public interface IWebhookEventRepository
{
    /// <summary>
    /// Attempts to insert an idempotency record for this webhook event.
    /// Returns <c>true</c> when the record was newly inserted (first delivery).
    /// Returns <c>false</c> when a record with the same key already exists (duplicate).
    /// Uses INSERT … ON CONFLICT DO NOTHING semantics to prevent the record-before-publish
    /// race: the caller must publish/process only when this returns <c>true</c>.
    /// </summary>
    /// <param name="idempotencyKey">Unique key identifying this webhook delivery.</param>
    /// <param name="eventType">Provider event type (e.g. "customer.subscription.updated").</param>
    /// <param name="providerName">Name of the webhook provider (e.g. "stripe").</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryRecordAsync(string idempotencyKey, string eventType, string providerName, CancellationToken ct = default);
}
