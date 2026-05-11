using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Lifecycle;

/// <summary>
/// Adapter interface that isolates Identity.Application from the Notifications module.
/// Implemented in Identity.Infrastructure using <c>INotificationDispatcher</c>.
/// </summary>
public interface INotificationDispatcherAdapter
{
    /// <summary>Sends an account-deletion confirmation email with a restore link.</summary>
    Task SendAccountDeletionEmailAsync(
        string toEmail,
        string displayName,
        string restoreLink,
        DateTimeOffset restoreLinkExpiresAt,
        CancellationToken cancellationToken = default);
}
