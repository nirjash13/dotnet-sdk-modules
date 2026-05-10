using System;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Default <see cref="IRateLimitProfileSelector"/> that returns a statically configured profile
/// for all tenants. Intended for use before Phase 4 entitlements are integrated.
/// </summary>
/// <remarks>
/// TODO(Phase 4 — Entitlements): Replace with an entitlement-aware selector.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 4.8.
/// </remarks>
public sealed class DefaultRateLimitProfileSelector : IRateLimitProfileSelector
{
    private readonly EditionRateLimitProfile _defaultProfile;

    /// <summary>
    /// Initializes the selector with the profile that applies to all tenants.
    /// </summary>
    /// <param name="defaultProfile">The profile to return for every tenant.</param>
    public DefaultRateLimitProfileSelector(EditionRateLimitProfile defaultProfile = EditionRateLimitProfile.Free)
    {
        _defaultProfile = defaultProfile;
    }

    /// <inheritdoc />
    /// <remarks>Returns the configured default profile regardless of the tenant.</remarks>
    public EditionRateLimitProfile GetProfile(Guid tenantId) => _defaultProfile;
}
