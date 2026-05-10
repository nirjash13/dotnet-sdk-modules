using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Entitlements.Domain;

namespace Entitlements.Application.Abstractions;

/// <summary>
/// Repository contract for reading <see cref="EntitlementGrant"/> records.
/// </summary>
public interface IEntitlementRepository
{
    /// <summary>
    /// Returns all entitlement grants for the given edition, plus any tenant-level overrides.
    /// Tenant overrides take precedence over edition grants for the same key.
    /// </summary>
    Task<IReadOnlyList<EntitlementGrant>> GetEffectiveGrantsAsync(
        Guid tenantId,
        Guid? editionId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns tenant-level override grants only (for admin inspection).
    /// </summary>
    Task<IReadOnlyList<EntitlementGrant>> GetTenantOverridesAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Persists a tenant-level override grant.
    /// </summary>
    Task AddOverrideAsync(EntitlementGrant grant, CancellationToken ct = default);
}
