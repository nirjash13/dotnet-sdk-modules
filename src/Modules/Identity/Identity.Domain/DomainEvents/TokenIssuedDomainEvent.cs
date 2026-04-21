using System;
using Chassis.SharedKernel.Abstractions;

namespace Identity.Domain.DomainEvents;

/// <summary>
/// Domain event raised when an access token is issued for a user or client.
/// </summary>
public sealed class TokenIssuedDomainEvent : IDomainEvent
{
    /// <summary>Initializes a new instance of <see cref="TokenIssuedDomainEvent"/>.</summary>
    public TokenIssuedDomainEvent(Guid userId, Guid tenantId, string grantType, DateTimeOffset issuedAt)
    {
        UserId = userId;
        TenantId = tenantId;
        GrantType = grantType;
        IssuedAt = issuedAt;
    }

    /// <summary>Gets the user identifier.</summary>
    public Guid UserId { get; }

    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the OAuth 2.0 grant type used to issue the token.</summary>
    public string GrantType { get; }

    /// <summary>Gets the UTC timestamp when the token was issued.</summary>
    public DateTimeOffset IssuedAt { get; }
}
