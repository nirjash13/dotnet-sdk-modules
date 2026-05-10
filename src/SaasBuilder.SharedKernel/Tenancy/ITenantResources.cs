using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SaasBuilder.SharedKernel.Tenancy;

/// <summary>
/// Describes the per-tenant infrastructure resources required for an isolated deployment.
/// Implementations vary by <see cref="Configuration.TenantIsolation"/> mode:
/// <list type="bullet">
///   <item><see cref="Configuration.TenantIsolation.PoolWithRls"/> — shared connection; container/index/stamp are <see langword="null"/>.</item>
///   <item><see cref="Configuration.TenantIsolation.SiloedDatabase"/> — distinct connection string per tenant.</item>
///   <item><see cref="Configuration.TenantIsolation.SiloedStamp"/> — regional stamp URI; requests routed to that stamp.</item>
/// </list>
/// </summary>
public interface ITenantResources
{
    /// <summary>Gets the connection string for this tenant's primary database.</summary>
    string ConnectionString { get; }

    /// <summary>
    /// Gets the blob storage container name scoped to this tenant, or <see langword="null"/>
    /// when blob storage is not per-tenant (e.g., <c>PoolWithRls</c> mode).
    /// </summary>
    string? BlobContainer { get; }

    /// <summary>
    /// Gets the search index name scoped to this tenant, or <see langword="null"/>
    /// when search indexing is not per-tenant.
    /// </summary>
    string? SearchIndex { get; }

    /// <summary>
    /// Gets the base URI of the regional stamp serving this tenant, or <see langword="null"/>
    /// for shared-deployment modes (<c>PoolWithRls</c>, <c>PoolShared</c>, <c>SiloedSchema</c>, <c>SiloedDatabase</c>).
    /// </summary>
    string? StampUri { get; }

    /// <summary>
    /// Gets arbitrary per-tenant routing metadata tags (e.g., region, tier, custom routing hints).
    /// Keys and values are application-defined; the SDK does not interpret them.
    /// </summary>
    IReadOnlyDictionary<string, string> Tags { get; }
}

/// <summary>
/// Resolves <see cref="ITenantResources"/> for a given tenant at request time.
/// Implementations are registered as scoped services so the resolution can be
/// request-aware (e.g., read from a per-request cache).
/// </summary>
public interface ITenantResourcesProvider
{
    /// <summary>
    /// Returns the <see cref="ITenantResources"/> record for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant for which resources are resolved.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The resolved <see cref="ITenantResources"/>.</returns>
    ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default);
}
