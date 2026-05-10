using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Authorization;

/// <summary>
/// Runtime lookup service for the permission tree.
/// Populated at startup by aggregating all <see cref="IPermissionDefinitionProvider"/> registrations.
/// </summary>
public interface IPermissionRegistry
{
    /// <summary>Returns all registered permission definitions.</summary>
    IReadOnlyList<PermissionDefinition> GetAll();

    /// <summary>Returns the definition for <paramref name="key"/>, or <see langword="null"/> if not found.</summary>
    PermissionDefinition? TryGet(string key);

    /// <summary>
    /// Returns the permission keys granted to the given role.
    /// </summary>
    /// <param name="roleId">The role to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
