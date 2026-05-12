namespace SaasBuilder.Host.RateLimiting;

/// <summary>
/// Marker options registered in the DI container when
/// <see cref="PerTenantSlidingWindow.AddPerTenantSlidingWindowRateLimiting"/> is called.
/// Used by <c>UseSaasBuilderPipeline</c> to conditionally activate
/// <see cref="PerTenantRateLimitMiddleware"/> without resolving the middleware type directly
/// (resolving a transient middleware from IServiceProvider as a presence check is unreliable
/// because the DI container always creates a new instance regardless of registration).
/// </summary>
public sealed class PerTenantRateLimitOptions
{
    /// <summary>
    /// Gets a value indicating whether the per-tenant rate-limit middleware is active.
    /// Defaults to <c>false</c>; set to <c>true</c> by <c>AddPerTenantSlidingWindowRateLimiting</c>.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>Soft-limit threshold (count of permits consumed at which the warning header is emitted).</summary>
    public int SoftLimitThreshold { get; init; }

    /// <summary>Configured permit limit per window.</summary>
    public int PermitLimit { get; init; }
}
