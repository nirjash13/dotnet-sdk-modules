using SaasBuilder.SharedKernel.Configuration;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Options controlling the tenant data isolation strategy.
/// </summary>
public sealed class SaasBuilderTenancyOptions
{
    /// <summary>
    /// Gets the selected isolation mode.
    /// Defaults to <see cref="TenantIsolation.PoolWithRls"/> (the only fully-implemented mode in Phase 1).
    /// </summary>
    public TenantIsolation Isolation { get; private set; } = TenantIsolation.PoolWithRls;

    /// <summary>
    /// Sets the isolation strategy to <see cref="TenantIsolation.PoolWithRls"/> (default).
    /// </summary>
    public SaasBuilderTenancyOptions UsePoolWithRls()
    {
        Isolation = TenantIsolation.PoolWithRls;
        return this;
    }

    /// <summary>
    /// Validates that the chosen isolation mode is supported in the current phase.
    /// Throws <see cref="NotSupportedException"/> for modes not yet implemented.
    /// </summary>
    internal void AssertSupported()
    {
        if (Isolation != TenantIsolation.PoolWithRls)
        {
            throw new NotSupportedException(
                $"TenantIsolation.{Isolation} is not yet implemented. " +
                "Only PoolWithRls is supported in Phase 1. " +
                "See Phase 3 of the roadmap for additional isolation modes.");
        }
    }
}
