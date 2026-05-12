using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Auth;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Auth;

/// <summary>
/// Account lockout service.
/// Locks accounts after <see cref="LockoutOptions.MaxFailedAttempts"/> consecutive
/// failures within <see cref="LockoutOptions.WindowMinutes"/> minutes.
/// Lockout duration: <see cref="LockoutOptions.LockoutDurationMinutes"/> minutes.
/// </summary>
public sealed class AccountLockoutService(
    IUserRepository userRepository,
    IOptions<LockoutOptions> options,
    ILogger<AccountLockoutService> logger)
    : IAccountLockoutService
{
    private readonly LockoutOptions _options = options.Value;

    /// <inheritdoc />
    public async Task RecordFailedAttemptAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // FindByIdForUpdateAsync: tracked entity required so EF Core persists the mutation.
        User? user = await userRepository.FindByIdForUpdateAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        user.RecordFailedLogin();

        if (user.FailedLoginAttempts >= _options.MaxFailedAttempts)
        {
            DateTimeOffset lockUntil = DateTimeOffset.UtcNow.AddMinutes(_options.LockoutDurationMinutes);
            user.LockOut(lockUntil);

            logger.LogWarning(
                "Account {UserId} locked out until {LockoutUntil} after {Attempts} failed attempts.",
                userId,
                lockUntil,
                user.FailedLoginAttempts);
        }

        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        User? user = await userRepository.FindByIdForUpdateAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        user.ResetFailedLogins();
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        User? user = await userRepository.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return user?.IsLockedOut ?? false;
    }

    /// <inheritdoc />
    public async Task AdminUnlockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        User? user = await userRepository.FindByIdForUpdateAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        user.AdminUnlock();
        await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Account {UserId} administratively unlocked.", userId);
    }
}
