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
    /// Records a processed webhook event so future duplicates are rejected.
    /// </summary>
    Task RecordAsync(string idempotencyKey, string eventType, CancellationToken ct = default);
}
