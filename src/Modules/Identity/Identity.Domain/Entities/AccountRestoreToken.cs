using System;

namespace Identity.Domain.Entities;

/// <summary>
/// Single-use token for restoring a soft-deleted account.
/// The raw token is emailed to the user at deletion time; only the hash is stored.
/// </summary>
public sealed class AccountRestoreToken
{
    private AccountRestoreToken()
    {
    }

    /// <summary>Gets the token identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the user this token belongs to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the SHA-256 hash of the raw token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Gets the UTC time this token expires.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the UTC time this token was consumed, or null if unused.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Creates a new restore token record (no raw token returned — caller must hash externally).</summary>
    public static AccountRestoreToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        return new AccountRestoreToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Marks the token as consumed.</summary>
    public void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;
}
