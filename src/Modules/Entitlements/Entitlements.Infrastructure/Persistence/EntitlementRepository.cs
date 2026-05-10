using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entitlements.Application.Abstractions;
using Entitlements.Domain;
using Microsoft.EntityFrameworkCore;

namespace Entitlements.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IEntitlementRepository"/>.
/// Tenant-level overrides take precedence over edition-level grants for the same key.
/// </summary>
internal sealed class EntitlementRepository(EntitlementsDbContext db) : IEntitlementRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<EntitlementGrant>> GetEffectiveGrantsAsync(
        Guid tenantId,
        Guid? editionId,
        CancellationToken ct)
    {
        // Load tenant-level overrides.
        List<EntitlementGrant> overrides = await db.EntitlementGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Load edition-level grants (if an edition is configured).
        List<EntitlementGrant> editionGrants = editionId.HasValue
            ? await db.EntitlementGrants
                .AsNoTracking()
                .Where(g => g.EditionId == editionId)
                .ToListAsync(ct)
                .ConfigureAwait(false)
            : new List<EntitlementGrant>();

        // Merge: tenant overrides win per key.
        HashSet<string> overrideKeys = new HashSet<string>(
            overrides.Select(o => o.Key),
            StringComparer.OrdinalIgnoreCase);

        List<EntitlementGrant> effective = new List<EntitlementGrant>(overrides);
        effective.AddRange(editionGrants.Where(g => !overrideKeys.Contains(g.Key)));

        return effective;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EntitlementGrant>> GetTenantOverridesAsync(
        Guid tenantId, CancellationToken ct)
        => await db.EntitlementGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.EditionId == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AddOverrideAsync(EntitlementGrant grant, CancellationToken ct)
    {
        db.EntitlementGrants.Add(grant);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
