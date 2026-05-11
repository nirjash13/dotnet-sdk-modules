using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Notifications;

/// <summary>
/// Default implementation of <see cref="INotificationDispatcherAdapter"/> that logs
/// the deletion email details. The host application can override this registration
/// with a production-grade dispatcher after <c>AddIdentityInfrastructure()</c>.
/// </summary>
public sealed class LoggingOnlyNotificationDispatcherAdapter(
    ILogger<LoggingOnlyNotificationDispatcherAdapter> logger)
    : INotificationDispatcherAdapter
{
    /// <inheritdoc />
    public Task SendAccountDeletionEmailAsync(
        string toEmail,
        string displayName,
        string restoreLink,
        DateTimeOffset restoreLinkExpiresAt,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Account deletion email (logging-only adapter): to={Email}, restoreLink={Link}, expires={Expires}.",
            toEmail,
            restoreLink,
            restoreLinkExpiresAt);

        return Task.CompletedTask;
    }
}
