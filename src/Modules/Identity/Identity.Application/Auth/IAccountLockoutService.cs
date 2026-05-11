using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Auth;

/// <summary>
/// Account lockout abstraction. Tracks failed login attempts and locks accounts
/// after the configured threshold. Lockout duration is configurable.
/// </summary>
public interface IAccountLockoutService
{
    /// <summary>
    /// Records a failed login attempt for the user.
    /// Automatically locks the account when the threshold is reached.
    /// </summary>
    Task RecordFailedAttemptAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the failed attempt counter on successful login.
    /// </summary>
    Task ResetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <see langword="true"/> if the account is currently locked out.
    /// </summary>
    Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Administratively unlocks the account. Requires the <c>users.unlock</c> permission.
    /// </summary>
    Task AdminUnlockAsync(Guid userId, CancellationToken cancellationToken = default);
}
