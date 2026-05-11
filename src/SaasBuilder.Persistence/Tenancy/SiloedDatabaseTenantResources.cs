using System;
using System.Collections.Generic;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// <see cref="ITenantResources"/> for the <c>SiloedDatabase</c> isolation mode.
/// Each tenant has a dedicated database identified by a distinct connection string.
/// </summary>
internal sealed class SiloedDatabaseTenantResources : ITenantResources
{
    private static readonly IReadOnlyDictionary<string, string> _emptyTags =
        new Dictionary<string, string>(StringComparer.Ordinal);

    internal SiloedDatabaseTenantResources(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));
        }

        ConnectionString = connectionString;
    }

    /// <inheritdoc />
    public string ConnectionString { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Blob container isolation in <c>SiloedDatabase</c> mode uses the tenant ID as the container name.
    /// The actual container must be pre-provisioned; this property returns the conventional name.
    /// </remarks>
    public string? BlobContainer => null;

    /// <inheritdoc />
    public string? SearchIndex => null;

    /// <inheritdoc />
    public string? StampUri => null;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Tags => _emptyTags;
}
