using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Authorization;

/// <summary>
/// Represents a named role that can be assigned to organization members.
/// System roles (IsSystem = true) are seeded and cannot be deleted.
/// Tenant-scoped custom roles have a non-null OrganizationId.
/// </summary>
public sealed class Role
{
    // Private constructor — use factory method.
    private Role()
    {
    }

    /// <summary>Gets the role's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the organization this role is scoped to, or <see langword="null"/> for system-wide roles.
    /// System roles (Owner, Admin, Member, ReadOnly) have no OrganizationId.
    /// </summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Gets the role name (e.g., "Owner", "Admin", "Member", "ReadOnly").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this is a built-in system role.
    /// System roles are seeded at startup and cannot be deleted.
    /// </summary>
    public bool IsSystem { get; private set; }

    /// <summary>Creates a new system (built-in) role.</summary>
    public static Role CreateSystem(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Role id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException("Role name must not be empty.");
        }

        return new Role
        {
            Id = id,
            OrganizationId = null,
            Name = name.Trim(),
            IsSystem = true,
        };
    }

    /// <summary>Creates a custom role scoped to an organization.</summary>
    public static Role CreateCustom(Guid id, Guid organizationId, string name)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Role id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("OrganizationId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException("Role name must not be empty.");
        }

        return new Role
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name.Trim(),
            IsSystem = false,
        };
    }
}
