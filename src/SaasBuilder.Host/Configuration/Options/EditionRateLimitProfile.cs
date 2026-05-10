namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Rate-limit tiers aligned with SaaS billing editions.
/// The active profile controls how many requests per window a tenant's edition is permitted.
/// </summary>
/// <remarks>
/// TODO(Phase 4 — Entitlements): Edition lookup integration is deferred. The default selector
/// returns the configured profile from options. When Phase 4 entitlements land, replace with
/// an <c>IEntitlementService.GetValueAsync&lt;EditionRateLimitProfile&gt;("rate_limit_edition")</c> call.
/// </remarks>
public enum EditionRateLimitProfile
{
    /// <summary>Lowest rate-limit tier — suitable for free-plan tenants.</summary>
    Free = 0,

    /// <summary>Standard rate-limit tier — suitable for paid plan tenants.</summary>
    Pro = 1,

    /// <summary>Highest rate-limit tier — suitable for enterprise tenants with dedicated SLAs.</summary>
    Enterprise = 2,
}
