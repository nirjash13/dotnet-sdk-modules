using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// Stub <see cref="ITenantResourcesProvider"/> for the <c>PoolShared</c> isolation mode.
/// This mode is intended for non-sensitive B2C data where all tenants share a pool without RLS.
/// </summary>
/// <remarks>
/// TODO(Phase 3.1): Implement PoolShared mode.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1 for the implementation plan.
/// Use <see cref="Configuration.TenantIsolation.PoolWithRls"/> in the meantime.
/// </remarks>
public sealed class PoolSharedTenantResourcesProvider : ITenantResourcesProvider
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown — this mode is not yet implemented.</exception>
    public ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.1): PoolShared isolation mode is deferred — " +
            "use PoolWithRls for v0.x. See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1.");
}
