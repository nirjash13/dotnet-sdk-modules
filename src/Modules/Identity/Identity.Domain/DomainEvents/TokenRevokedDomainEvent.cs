using System;
using Chassis.SharedKernel.Abstractions;

namespace Identity.Domain.DomainEvents;

/// <summary>
/// Domain event raised when an access or refresh token is explicitly revoked.
/// </summary>
public sealed class TokenRevokedDomainEvent : IDomainEvent
{
    /// <summary>Initializes a new instance of <see cref="TokenRevokedDomainEvent"/>.</summary>
    public TokenRevokedDomainEvent(Guid tokenId, string subject, DateTimeOffset revokedAt)
    {
        TokenId = tokenId;
        Subject = subject;
        RevokedAt = revokedAt;
    }

    /// <summary>Gets the token identifier.</summary>
    public Guid TokenId { get; }

    /// <summary>Gets the subject claim from the revoked token.</summary>
    public string Subject { get; }

    /// <summary>Gets the UTC timestamp when the token was revoked.</summary>
    public DateTimeOffset RevokedAt { get; }
}
