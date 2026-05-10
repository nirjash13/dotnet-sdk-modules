using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Entitlements.Application.Abstractions;
using Entitlements.Domain;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace Entitlements.Application;

/// <summary>
/// Default <see cref="IEntitlementService"/> implementation.
/// Reads entitlement grants from cache → DB → active edition.
/// Cache key: (tenantId, entitlementKey). Invalidated on SubscriptionUpdated.
///
/// TODO(Phase 4): Replace the in-process ConcurrentDictionary cache with IDistributedCache (Redis)
/// for multi-instance deployments. Subscribe to SubscriptionUpdatedIntegrationEvent via MassTransit
/// to invalidate the cache entry for the affected tenant.
/// </summary>
public sealed class EntitlementService(
    IEntitlementRepository repository,
    ITenantContextAccessor tenantAccessor,
    ILogger<EntitlementService> logger)
    : IEntitlementService
{
    // Simple in-process cache: (tenantId, key) → grants snapshot.
    // In production this should be IDistributedCache with a short TTL.
    private readonly ConcurrentDictionary<(Guid, Guid?), IReadOnlyList<EntitlementGrant>> _cache = new();

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

    /// <summary>Invalidates the cache for the specified tenant (called on subscription update).</summary>
    public void InvalidateCache(Guid tenantId)
    {
        foreach ((Guid tid, Guid? _) key in _cache.Keys.Where(k => k.Item1 == tenantId).ToList())
        {
            _cache.TryRemove(key, out _);
        }

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

        // TODO(Phase 4): Resolve the active edition ID from the Billing module's subscription.
        // For scaffold: editionId is null so only tenant-level overrides are returned from the repository.
        Guid? editionId = null;
        var cacheKey = (tenant.TenantId, editionId);

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<EntitlementGrant>? cached))
        {
            return cached;
        }

        IReadOnlyList<EntitlementGrant> grants = await repository
            .GetEffectiveGrantsAsync(tenant.TenantId, editionId, ct)
            .ConfigureAwait(false);

        _cache[cacheKey] = grants;
        return grants;
    }
}
