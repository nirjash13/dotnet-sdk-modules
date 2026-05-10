using System;

namespace Identity.Domain.Authorization;

/// <summary>
/// Join entity linking a <see cref="Role"/> to a <see cref="Permission"/>.
/// </summary>
public sealed class RolePermission
{
    // Private constructor — EF Core materialises via this.
    private RolePermission()
    {
    }

    /// <summary>Gets the role identifier.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>Gets the permission identifier.</summary>
    public Guid PermissionId { get; private set; }

    /// <summary>Creates a new <see cref="RolePermission"/> link.</summary>
    public static RolePermission Create(Guid roleId, Guid permissionId) =>
        new RolePermission { RoleId = roleId, PermissionId = permissionId };
}
