using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Services;
using Identity.Domain.Entities;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Lifecycle;

/// <summary>
/// Handles <see cref="RequestAccountDeletionCommand"/>.
/// Soft-deletes the user account, generates a restore token, and sends a restoration email.
/// </summary>
public sealed class RequestAccountDeletionHandler
{
    private readonly IUserRepository _users;
    private readonly IAccountRestoreTokenStore _tokenStore;
    private readonly INotificationDispatcherAdapter _notificationDispatcher;

    /// <summary>Initializes a new instance of <see cref="RequestAccountDeletionHandler"/>.</summary>
    public RequestAccountDeletionHandler(
        IUserRepository users,
        IAccountRestoreTokenStore tokenStore,
        INotificationDispatcherAdapter notificationDispatcher)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _notificationDispatcher = notificationDispatcher ?? throw new ArgumentNullException(nameof(notificationDispatcher));
    }

    /// <summary>Handles the command.</summary>
    public async Task<Result> HandleAsync(
        RequestAccountDeletionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        User? user = await _users.FindByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        if (user.IsDeleted)
        {
            return Result.Failure("Account is already scheduled for deletion.");
        }

        user.RequestDeletion(gracePeriodDays: 30);
        await _users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset expiresAt = user.DeletionScheduledFor!.Value;
        string rawToken = await _tokenStore
            .CreateAsync(user.Id, expiresAt, cancellationToken)
            .ConfigureAwait(false);

        string restoreLink = $"{command.RestoreBaseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(rawToken)}";

        // Fire-and-forget is intentional; email failure must not roll back the deletion state.
        await _notificationDispatcher.SendAccountDeletionEmailAsync(
            user.Email,
            user.DisplayName,
            restoreLink,
            expiresAt,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
