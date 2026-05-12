using System.Threading;
using System.Threading.Tasks;

namespace Billing.Application.Abstractions;

/// <summary>
/// Repository for idempotency deduplication of inbound webhook events.
/// </summary>
public interface IWebhookEventRepository
{
    /// <summary>
    /// Returns true if a webhook event with the given idempotency key has already been processed.
    /// </summary>
    Task<bool> ExistsAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>
    /// Attempts to insert an idempotency record for this webhook event.
    /// Returns <c>true</c> when the record was newly inserted (first delivery).
    /// Returns <c>false</c> when a record with the same key already exists (duplicate).
    /// Uses INSERT … ON CONFLICT DO NOTHING semantics to prevent the record-before-publish
    /// race: the caller must publish/process only when this returns <c>true</c>.
    /// </summary>
    Task<bool> TryRecordAsync(string idempotencyKey, string eventType, CancellationToken ct = default);
}
