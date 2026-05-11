using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Entities;

/// <summary>
/// Represents an active or completed admin impersonation session.
/// The session is time-boxed (max 1 hour) and carries a mandatory reason field.
/// </summary>
public sealed class ImpersonationSessionEntity
{
    private ImpersonationSessionEntity()
    {
    }

    /// <summary>Gets the session's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the system admin performing the impersonation.</summary>
    public Guid AdminUserId { get; private set; }

    /// <summary>Gets the user being impersonated.</summary>
    public Guid TargetUserId { get; private set; }

    /// <summary>Gets the mandatory reason provided by the admin.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Gets the short-lived impersonation token (JWT with act claim).</summary>
    public string ImpersonationToken { get; private set; } = string.Empty;

    /// <summary>Gets the UTC time at which the session expires (max 1h).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Gets the UTC time the session started.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Gets the UTC time the session ended, or <see langword="null"/> if still active.</summary>
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Gets a value indicating whether this session is currently active.</summary>
    public bool IsActive => EndedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    /// <summary>Creates a new impersonation session record.</summary>
    public static ImpersonationSessionEntity Create(
        Guid id,
        Guid adminUserId,
        Guid targetUserId,
        string reason,
        string impersonationToken,
        DateTimeOffset expiresAt)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Session id must not be empty.");
        }

        if (adminUserId == Guid.Empty)
        {
            throw new IdentityDomainException("Admin user id must not be empty.");
        }

        if (targetUserId == Guid.Empty)
        {
            throw new IdentityDomainException("Target user id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new IdentityDomainException("Impersonation reason must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(impersonationToken))
        {
            throw new IdentityDomainException("Impersonation token must not be empty.");
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new IdentityDomainException("Session expiry must be in the future.");
        }

        return new ImpersonationSessionEntity
        {
            Id = id,
            AdminUserId = adminUserId,
            TargetUserId = targetUserId,
            Reason = reason.Trim(),
            ImpersonationToken = impersonationToken,
            StartedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>Ends the session.</summary>
    public void End()
    {
        if (EndedAt is not null)
        {
            throw new IdentityDomainException("Impersonation session is already ended.");
        }

        EndedAt = DateTimeOffset.UtcNow;
    }
}
