using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// Routes tenant requests to a regional stamp (deployment unit).
/// Implement this interface to map a tenant identifier to the base URI of the stamp
/// that owns the tenant's data.
/// </summary>
/// <remarks>
/// TODO(Phase 3.1): Implement IStampRouter. See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1.
/// </remarks>
public interface IStampRouter
{
    /// <summary>
    /// Returns the base URI of the stamp serving the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant to route.</param>
    /// <returns>The stamp base URI.</returns>
    /// <exception cref="NotSupportedException">Always thrown — deferred to Phase 3.</exception>
    Uri RouteFor(Guid tenantId);
}

/// <summary>
/// Stub <see cref="ITenantResourcesProvider"/> for the <c>SiloedStamp</c> isolation mode.
/// Each tenant is pinned to a regional deployment stamp; <see cref="IStampRouter"/> maps
/// the tenant to the correct stamp URI.
/// </summary>
/// <remarks>
/// TODO(Phase 3.1): Implement SiloedStamp mode with regional stamp routing.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1.
/// Use <see cref="Configuration.TenantIsolation.PoolWithRls"/> in the meantime.
/// </remarks>
public sealed class SiloedStampTenantResourcesProvider : ITenantResourcesProvider
{
    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always thrown — this mode is not yet implemented.</exception>
    public ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default)
        => throw new NotSupportedException(
            "TODO(Phase 3.1): SiloedStamp isolation mode is deferred — " +
            "use PoolWithRls for v0.x. See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.1.");
}
