using System.Threading;
using System.Threading.Tasks;
using Notifications.Contracts;

namespace Notifications.Application.Abstractions;

/// <summary>
/// Dispatches a <see cref="NotificationMessage"/> to all requested channels.
/// Implementations must be silent-degrading: when a channel is unavailable,
/// log a WARNING and continue — do not throw.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="message"/> to its requested channels.
    /// Always completes successfully — failures are logged, not propagated.
    /// </summary>
    /// <param name="message">The notification to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchAsync(NotificationMessage message, CancellationToken ct = default);
}
