using System.Threading;
using System.Threading.Tasks;
using Notifications.Contracts;

namespace Notifications.Application.Abstractions;

/// <summary>
/// Bounce and suppression list — prevents delivery to addresses/numbers that have
/// previously hard-bounced, unsubscribed, or been manually suppressed.
/// TODO(Phase 5.1): wire to provider bounce webhooks (SendGrid, SES, etc.)
/// </summary>
public interface ISuppressionList
{
    /// <summary>Returns <see langword="true"/> when <paramref name="address"/> is suppressed for <paramref name="channel"/>.</summary>
    Task<bool> IsSuppressedAsync(string address, NotificationChannel channel, CancellationToken ct = default);

    /// <summary>Adds <paramref name="address"/> to the suppression list for <paramref name="channel"/>.</summary>
    Task SuppressAsync(string address, NotificationChannel channel, string reason, CancellationToken ct = default);
}
