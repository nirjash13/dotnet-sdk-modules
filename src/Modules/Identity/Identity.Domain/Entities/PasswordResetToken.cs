using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Entities;

/// <summary>
/// Single-use password reset token, hashed at rest.
/// Valid for 1 hour from creation. Replay-protected via <see cref="UsedAt"/>.
/// </summary>
public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
    }

    /// <summary>Gets the token's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the user this token belongs to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the SHA-256 hash of the raw token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Gets the UTC expiry time (1 h from creation).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the UTC time the token was consumed, or <see langword="null"/> if unused.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Gets a value indicating whether the token is still valid.</summary>
    public bool IsValid => UsedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    /// <summary>Creates a new <see cref="PasswordResetToken"/>.</summary>
    public static PasswordResetToken Create(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("PasswordResetToken id must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("UserId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new IdentityDomainException("Token hash must not be empty.");
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new IdentityDomainException("Token expiry must be in the future.");
        }

        return new PasswordResetToken
        {
            Id = id,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Marks the token as consumed, preventing replay.</summary>
    public void MarkUsed()
    {
        if (!IsValid)
        {
            throw new IdentityDomainException("Password reset token is expired or already used.");
        }

        UsedAt = DateTimeOffset.UtcNow;
    }
}
