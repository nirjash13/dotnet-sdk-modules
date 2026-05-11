using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Lifecycle;

/// <summary>
/// Represents a restore-token record returned from <see cref="IAccountRestoreTokenStore"/>.
/// </summary>
/// <param name="UserId">The user the token belongs to.</param>
/// <param name="ExpiresAt">UTC expiry of the token.</param>
public sealed record AccountRestoreTokenEntry(Guid UserId, DateTimeOffset ExpiresAt);

/// <summary>
/// Stores and validates single-use account restore tokens.
/// Tokens are valid for the same duration as the deletion grace period (default 30 days).
/// </summary>
public interface IAccountRestoreTokenStore
{
    /// <summary>
    /// Generates and persists a new single-use restore token for the user.
    /// Returns the raw (unhashed) token to be included in the restore email link.
    /// </summary>
    Task<string> CreateAsync(Guid userId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the raw token and returns the associated entry if valid and not yet used.
    /// Marks the token as used atomically.
    /// Returns null if the token is invalid, expired, or already used.
    /// </summary>
    Task<AccountRestoreTokenEntry?> ConsumeAsync(string rawToken, CancellationToken cancellationToken = default);
}
