using System;
using System.Collections.Generic;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// <see cref="ITenantResources"/> for the <c>SiloedSchema</c> isolation mode.
/// All tenants share the same database; each has a dedicated schema.
/// The connection string is the shared pool connection; the schema name is passed
/// as a tag so that EF Core migrations and queries can target the right schema.
/// </summary>
internal sealed class SiloedSchemaTenantResources : ITenantResources
{
    /// <summary>Tag key used to carry the schema name.</summary>
    public const string SchemaTagKey = "schema";

    internal SiloedSchemaTenantResources(string connectionString, string schemaName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new ArgumentException("Schema name must not be null or empty.", nameof(schemaName));
        }

        ConnectionString = connectionString;
        Tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SchemaTagKey] = schemaName,
        };
    }

    /// <inheritdoc />
    public string ConnectionString { get; }

    /// <inheritdoc />
    public string? BlobContainer => null;

    /// <inheritdoc />
    public string? SearchIndex => null;

    /// <inheritdoc />
    public string? StampUri => null;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Tags { get; }
}
