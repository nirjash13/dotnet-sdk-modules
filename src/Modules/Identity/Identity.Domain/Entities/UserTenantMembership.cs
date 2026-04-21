using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Entities;

/// <summary>
/// Represents a user's membership in a tenant, including the roles they hold within that tenant.
/// Used by <see cref="Identity.Infrastructure.Claims.TenantClaimEnricher"/> to inject
/// <c>tenant_id</c>, <c>roles</c>, and <c>membership_id</c> into the issued JWT.
/// </summary>
public sealed class UserTenantMembership
{
    // Private constructor — use factory method.
    private UserTenantMembership()
    {
    }

    /// <summary>Gets the unique identifier of this membership record.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the user this membership belongs to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the tenant this membership grants access to.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this is the user's primary tenant.
    /// The token enricher uses the primary membership to populate token claims (v1 single-tenant-per-token).
    /// </summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Gets the roles the user holds within this tenant.</summary>
    public string[] Roles { get; private set; } = Array.Empty<string>();

    /// <summary>Gets the UTC timestamp when this membership was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates a new <see cref="UserTenantMembership"/>.
    /// </summary>
    /// <param name="id">Unique membership identifier.</param>
    /// <param name="userId">The user's identifier.</param>
    /// <param name="tenantId">The tenant's identifier.</param>
    /// <param name="roles">Roles within this tenant.</param>
    /// <param name="isPrimary">Whether this is the user's primary tenant.</param>
    /// <returns>A new validated <see cref="UserTenantMembership"/>.</returns>
    public static UserTenantMembership Create(Guid id, Guid userId, Guid tenantId, string[] roles, bool isPrimary)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Membership id must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("UserId must not be empty.");
        }

        if (tenantId == Guid.Empty)
        {
            throw new IdentityDomainException("TenantId must not be empty.");
        }

        return new UserTenantMembership
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            Roles = roles ?? Array.Empty<string>(),
            IsPrimary = isPrimary,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
