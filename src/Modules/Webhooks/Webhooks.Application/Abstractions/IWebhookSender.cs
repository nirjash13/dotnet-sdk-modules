using System.Threading;
using System.Threading.Tasks;
using Webhooks.Domain.Entities;

namespace Webhooks.Application.Abstractions;

/// <summary>
/// Fans a <see cref="WebhookEvent"/> out to all active endpoints subscribed to its event type.
/// Signs each delivery with the Standard Webhooks signature scheme and retries on failure.
/// </summary>
public interface IWebhookSender
{
    /// <summary>
    /// Delivers <paramref name="evt"/> to all matching endpoints for the tenant on the event.
    /// </summary>
    /// <param name="evt">The event to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(WebhookEvent evt, CancellationToken ct = default);
}
