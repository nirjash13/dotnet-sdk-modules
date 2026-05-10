using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Contracts;

namespace Notifications.Infrastructure.Dispatchers;

/// <summary>
/// Silent no-op dispatcher registered when SMTP (or other channel) configuration is absent.
/// Logs a WARNING per dispatch so operators are aware notifications are not delivered.
/// Fulfils the silent-degradation contract: <see cref="INotificationDispatcher.DispatchAsync"/>
/// never throws; the host continues operating normally.
/// </summary>
internal sealed class NoOpNotificationDispatcher(ILogger<NoOpNotificationDispatcher> logger)
    : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        logger.LogWarning(
            "Notifications.NoOp: notification not delivered " +
            "(NotificationType={NotificationType}, Recipient={UserId}, Channels={Channels}). " +
            "Configure Notifications:Smtp:Host to enable email delivery.",
            message.NotificationType,
            message.RecipientUserId,
            string.Join(",", message.Channels));

        return Task.CompletedTask;
    }
}
