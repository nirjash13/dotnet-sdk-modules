using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// Stub <see cref="ITenantResourcesProvider"/> for the <c>SiloedDatabase</c> isolation mode.
/// Each tenant has a dedicated database; connection strings are resolved via this provider
/// and migrations are run per-database.
/// </summary>
/// <remarks>
/// TODO(Phase 3.1): Implement SiloedDatabase mode — per-tenant connection string lookup
/// and migration runner. See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1.
/// Use <see cref="Configuration.TenantIsolation.PoolWithRls"/> in the meantime.
/// </remarks>
public sealed class SiloedDatabaseTenantResourcesProvider : ITenantResourcesProvider
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown — this mode is not yet implemented.</exception>
    public ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.1): SiloedDatabase isolation mode is deferred — " +
            "use PoolWithRls for v0.x. See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1.");
}
