using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// Stub <see cref="ITenantResourcesProvider"/> for the <c>SiloedSchema</c> isolation mode.
/// Each tenant has a dedicated schema within a shared database; EF Core migrations are
/// targeted per schema.
/// </summary>
/// <remarks>
/// TODO(Phase 3.1): Implement SiloedSchema mode — schema-per-tenant with targeted EF Core migrations.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1 for the implementation plan.
/// Use <see cref="Configuration.TenantIsolation.PoolWithRls"/> in the meantime.
/// </remarks>
public sealed class SiloedSchemaTenantResourcesProvider : ITenantResourcesProvider
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown — this mode is not yet implemented.</exception>
    public ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.1): SiloedSchema isolation mode is deferred — " +
            "use PoolWithRls for v0.x. See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1.");
}
