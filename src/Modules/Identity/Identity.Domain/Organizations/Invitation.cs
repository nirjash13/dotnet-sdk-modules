using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Organizations;

/// <summary>
/// Represents a pending email invitation for a user to join an <see cref="Organization"/>.
/// </summary>
/// <remarks>
/// The token is stored in hashed form. The raw token is returned only at creation
/// and must be transmitted to the invitee via email. It is never stored in plain text.
/// </remarks>
public sealed class Invitation
{
    // Private constructor — use factory method.
    private Invitation()
    {
    }

    /// <summary>Gets the invitation's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the organization this invitation is for.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Gets the email address of the invitee.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Gets the role the invitee will assume on acceptance.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>
    /// Gets the hashed form of the invitation token.
    /// The raw (pre-hash) token is transmitted to the invitee via email and never stored.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>Gets the UTC expiry of this invitation.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the UTC timestamp when the invitation was redeemed, or <see langword="null"/> if pending.</summary>
    public DateTimeOffset? RedeemedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when the invitation was revoked, or <see langword="null"/> if active.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Gets the user id of the person who created this invitation.</summary>
    public Guid CreatedById { get; private set; }

    /// <summary>Gets a value indicating whether this invitation is still valid (not expired, not redeemed, not revoked).</summary>
    public bool IsActive => RedeemedAt is null && RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    /// <summary>
    /// Creates a new <see cref="Invitation"/>.
    /// </summary>
    /// <param name="id">Unique identifier.</param>
    /// <param name="organizationId">The target organization.</param>
    /// <param name="email">The invitee's email address.</param>
    /// <param name="roleId">The role to assign on acceptance.</param>
    /// <param name="tokenHash">The hashed invitation token.</param>
    /// <param name="expiresAt">When the invitation expires.</param>
    /// <param name="createdById">The user sending the invitation.</param>
    public static Invitation Create(
        Guid id,
        Guid organizationId,
        string email,
        Guid roleId,
        string tokenHash,
        DateTimeOffset expiresAt,
        Guid createdById)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Invitation id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("OrganizationId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new IdentityDomainException("Invitation email must not be empty.");
        }

        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new IdentityDomainException("Token hash must not be empty.");
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new IdentityDomainException("Invitation expiry must be in the future.");
        }

        if (createdById == Guid.Empty)
        {
            throw new IdentityDomainException("CreatedById must not be empty.");
        }

        return new Invitation
        {
            Id = id,
            OrganizationId = organizationId,
            Email = email.Trim().ToLowerInvariant(),
            RoleId = roleId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedById = createdById,
        };
    }

    /// <summary>Marks this invitation as redeemed.</summary>
    public void Redeem()
    {
        if (!IsActive)
        {
            throw new IdentityDomainException("Invitation is no longer active.");
        }

        RedeemedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Revokes this invitation, preventing future acceptance.</summary>
    public void Revoke()
    {
        if (RedeemedAt is not null)
        {
            throw new IdentityDomainException("Cannot revoke an already-redeemed invitation.");
        }

        RevokedAt = DateTimeOffset.UtcNow;
    }
}
