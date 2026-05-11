using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Entities;

/// <summary>
/// Single-use email verification token, hashed at rest.
/// Valid for 24 hours from creation. Implements replay protection via <see cref="UsedAt"/>.
/// </summary>
public sealed class EmailVerificationToken
{
    // Private constructor — use factory method.
    private EmailVerificationToken()
    {
    }

    /// <summary>Gets the token's unique identifier (lookup key).</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the user this token is for.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the SHA-256 hash of the raw token (stored; raw token sent via email).</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Gets the UTC time at which the token expires (24 h from creation).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the UTC time at which the token was used, or <see langword="null"/> if unused.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Gets a value indicating whether the token is still valid.</summary>
    public bool IsValid => UsedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    /// <summary>Creates a new <see cref="EmailVerificationToken"/>.</summary>
    public static EmailVerificationToken Create(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("EmailVerificationToken id must not be empty.");
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

        return new EmailVerificationToken
        {
            Id = id,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Marks the token as used, preventing replay.</summary>
    public void MarkUsed()
    {
        if (!IsValid)
        {
            throw new IdentityDomainException("Token is expired or already used.");
        }

        UsedAt = DateTimeOffset.UtcNow;
    }
}
