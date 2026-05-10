namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Options controlling the rate-limiting policy for auth and tenant-scoped endpoints.
/// </summary>
public sealed class SaasBuilderRateLimitingOptions
{
    /// <summary>Gets a value indicating whether rate limiting is enabled. Defaults to <c>true</c>.</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether per-tenant sliding-window limiting is active.
    /// Defaults to <c>false</c> (fixed-window per client IP / client_id).
    /// </summary>
    public bool UsePerTenantWindow { get; private set; }

    /// <summary>
    /// Enables rate limiting with a per-tenant sliding-window policy.
    /// Each tenant's request budget resets independently.
    /// </summary>
    public SaasBuilderRateLimitingOptions UsePerTenantSlidingWindow()
    {
        IsEnabled = true;
        UsePerTenantWindow = true;
        return this;
    }

    /// <summary>Disables rate limiting entirely. Not recommended for production.</summary>
    public SaasBuilderRateLimitingOptions Disable()
    {
        IsEnabled = false;
        return this;
    }
}
