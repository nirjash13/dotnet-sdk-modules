using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Services;
using Identity.Domain.Entities;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Lifecycle;

/// <summary>
/// Handles <see cref="RestoreAccountCommand"/>.
/// Validates the single-use restore token and cancels the scheduled deletion.
/// </summary>
public sealed class RestoreAccountHandler
{
    private readonly IUserRepository _users;
    private readonly IAccountRestoreTokenStore _tokenStore;

    /// <summary>Initializes a new instance of <see cref="RestoreAccountHandler"/>.</summary>
    public RestoreAccountHandler(
        IUserRepository users,
        IAccountRestoreTokenStore tokenStore)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    }

    /// <summary>Handles the command.</summary>
    public async Task<Result> HandleAsync(
        RestoreAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RawToken))
        {
            return Result.Failure("Restore token is required.");
        }

        AccountRestoreTokenEntry? entry = await _tokenStore
            .ConsumeAsync(command.RawToken, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return Result.Failure("Restore token is invalid, expired, or has already been used.");
        }

        User? user = await _users.FindByIdAsync(entry.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure("User not found.");
        }

        if (!user.IsDeleted)
        {
            // Already restored (idempotent).
            return Result.Success();
        }

        user.RestoreAccount();
        await _users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
