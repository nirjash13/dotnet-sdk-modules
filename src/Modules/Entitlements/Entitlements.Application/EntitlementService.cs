using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entitlements.Application.Abstractions;
using Entitlements.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace Entitlements.Application;

/// <summary>
/// Default <see cref="IEntitlementService"/> implementation.
///
/// Evaluation order:
/// 1. Return from IMemoryCache (5-minute TTL) when present.
/// 2. Load effective grants from DB via <see cref="IEntitlementRepository"/>.
/// 3. Cache the result; return.
///
/// Cache invalidation: call <see cref="InvalidateCache"/> when a
/// <c>SubscriptionUpdatedIntegrationEvent</c> is received for the tenant.
/// Register a MassTransit consumer in Infrastructure that calls this method.
/// </summary>
public sealed class EntitlementService(
    IEntitlementRepository repository,
    ITenantContextAccessor tenantAccessor,
    IMemoryCache cache,
    ILogger<EntitlementService> logger)
    : IEntitlementService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static string BuildCacheKey(Guid tenantId) => $"entitlements:{tenantId:D}";

    /// <inheritdoc />
    public async Task<bool> HasAsync(string key, CancellationToken ct)
    {
        IReadOnlyList<EntitlementGrant> grants = await GetGrantsAsync(ct).ConfigureAwait(false);

        EntitlementGrant? grant = grants.FirstOrDefault(g =>
            string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase) &&
            g.Type == EntitlementType.Boolean);

        return grant?.BoolValue ?? false;
    }

    /// <inheritdoc />
    public async Task<long?> GetLimitAsync(string key, CancellationToken ct)
    {
        IReadOnlyList<EntitlementGrant> grants = await GetGrantsAsync(ct).ConfigureAwait(false);

        EntitlementGrant? grant = grants.FirstOrDefault(g =>
            string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase) &&
            g.Type == EntitlementType.Numeric);

        return grant?.NumericLimit;
    }

    /// <inheritdoc />
    public async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        IReadOnlyList<EntitlementGrant> grants = await GetGrantsAsync(ct).ConfigureAwait(false);

        EntitlementGrant? grant = grants.FirstOrDefault(g =>
            string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase) &&
            g.Type == EntitlementType.String);

        return grant?.StringValue;
    }

    /// <summary>
    /// Invalidates the entitlement cache for the specified tenant.
    /// Called by the MassTransit consumer for <c>SubscriptionUpdatedIntegrationEvent</c>.
    /// </summary>
    public void InvalidateCache(Guid tenantId)
    {
        // Remove all cache entries for this tenant. The tenant's grants are stored under
        // a single composite key regardless of edition ID.
        cache.Remove(BuildCacheKey(tenantId));
        logger.LogDebug("Entitlement cache invalidated for tenant {TenantId}.", tenantId);
    }

    private async Task<IReadOnlyList<EntitlementGrant>> GetGrantsAsync(CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            logger.LogWarning("EntitlementService called without a tenant context — returning empty grants.");
            return Array.Empty<EntitlementGrant>();
        }

        string cacheKey = BuildCacheKey(tenant.TenantId);

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<EntitlementGrant>? cached) && cached is not null)
        {
            return cached;
        }

        // TODO(Phase 4.x): resolve editionId from Billing module's active subscription.
        // For now pass null so the repository returns all tenant-level overrides.
        IReadOnlyList<EntitlementGrant> grants = await repository
            .GetEffectiveGrantsAsync(tenant.TenantId, editionId: null, ct)
            .ConfigureAwait(false);

        cache.Set(cacheKey, grants, CacheTtl);
        return grants;
    }
}
