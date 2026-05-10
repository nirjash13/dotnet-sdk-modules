using System.Collections.Generic;

namespace Identity.Application.Authorization;

/// <summary>
/// Implemented by each module to declare the permissions it owns.
/// All implementations are discovered at startup and loaded into <see cref="IPermissionRegistry"/>.
/// </summary>
public interface IPermissionDefinitionProvider
{
    /// <summary>Returns all permission definitions owned by this provider.</summary>
    IEnumerable<PermissionDefinition> Define();
}
