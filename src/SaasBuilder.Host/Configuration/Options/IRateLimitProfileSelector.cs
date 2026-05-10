using System;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Selects the <see cref="EditionRateLimitProfile"/> to apply for a given tenant.
/// Implementations may consult the billing subscription, a cache, or the configured default.
/// </summary>
/// <remarks>
/// TODO(Phase 4 — Entitlements): Replace the default implementation with an entitlement-aware
/// selector that queries <c>IEntitlementService</c> for the tenant's current edition.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 4.8.
/// </remarks>
public interface IRateLimitProfileSelector
{
    /// <summary>
    /// Returns the rate-limit profile for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant for which the profile is selected.</param>
    /// <returns>The <see cref="EditionRateLimitProfile"/> to apply.</returns>
    EditionRateLimitProfile GetProfile(Guid tenantId);
}
