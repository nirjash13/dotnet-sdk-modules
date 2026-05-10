using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Authorization;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Authorization;

/// <summary>
/// EF Core-backed implementation of <see cref="IPermissionRegistry"/>.
/// </summary>
internal sealed class PermissionRegistry : IPermissionRegistry
{
    private readonly IReadOnlyList<PermissionDefinition> _allDefinitions;
    private readonly IdentityDbContext _context;

    /// <summary>Initializes a new instance of <see cref="PermissionRegistry"/>.</summary>
    public PermissionRegistry(
        IEnumerable<IPermissionDefinitionProvider> providers,
        IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        _allDefinitions = (providers ?? throw new ArgumentNullException(nameof(providers)))
            .SelectMany(p => p.Define())
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<PermissionDefinition> GetAll() => _allDefinitions;

    /// <inheritdoc />
    public PermissionDefinition? TryGet(string key) =>
        _allDefinitions.FirstOrDefault(d => d.Key == key);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        // Project directly to key strings — never return entities.
        List<string> keys = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Join(
                _context.Permissions.AsNoTracking(),
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => p.Resource + "." + p.Action + ":" + p.Scope)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return keys.AsReadOnly();
    }
}
