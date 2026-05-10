using System;
using System.Collections.Generic;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// <see cref="ITenantResources"/> implementation for <c>PoolWithRls</c> isolation mode.
/// All tenants share the same connection string; blob container, search index, and stamp URI
/// are not tenant-specific in the shared-pool model.
/// </summary>
internal sealed class PoolWithRlsTenantResources : ITenantResources
{
    private static readonly IReadOnlyDictionary<string, string> _emptyTags =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of <see cref="PoolWithRlsTenantResources"/> with the
    /// shared connection string.
    /// </summary>
    /// <param name="connectionString">
    /// The shared Postgres connection string for the pool. Must not be null or empty.
    /// </param>
    public PoolWithRlsTenantResources(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException(
                "Connection string must not be null or whitespace.",
                nameof(connectionString));
        }

        ConnectionString = connectionString;
    }

    /// <inheritdoc />
    public string ConnectionString { get; }

    /// <inheritdoc />
    /// <remarks>Always <see langword="null"/> in <c>PoolWithRls</c> mode — blob storage is not per-tenant.</remarks>
    public string? BlobContainer => null;

    /// <inheritdoc />
    /// <remarks>Always <see langword="null"/> in <c>PoolWithRls</c> mode — search index is not per-tenant.</remarks>
    public string? SearchIndex => null;

    /// <inheritdoc />
    /// <remarks>Always <see langword="null"/> in <c>PoolWithRls</c> mode — no regional stamp.</remarks>
    public string? StampUri => null;

    /// <inheritdoc />
    /// <remarks>Always empty in <c>PoolWithRls</c> mode.</remarks>
    public IReadOnlyDictionary<string, string> Tags => _emptyTags;
}
