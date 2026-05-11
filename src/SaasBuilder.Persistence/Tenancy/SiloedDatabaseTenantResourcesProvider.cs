using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// <see cref="ITenantResourcesProvider"/> for the <c>SiloedDatabase</c> isolation mode.
/// Resolves the per-tenant connection string from configuration using the convention:
/// <c>Tenancy:SiloedDatabase:{tenantId}:ConnectionString</c>.
/// Falls back to <c>ConnectionStrings:DefaultConnection</c> when no per-tenant entry is found
/// so that existing tenants are not broken during a pool-to-silo migration.
/// </summary>
/// <remarks>
/// <para>
/// Configuration example (appsettings.json or environment variables):
/// <code>
/// "Tenancy": {
///   "SiloedDatabase": {
///     "aaaaaaaa-0000-0000-0000-000000000001": {
///       "ConnectionString": "Host=acme-db.example.com;Database=acme_tenant;..."
///     }
///   }
/// }
/// </code>
/// Environment variable equivalent:
/// <c>Tenancy__SiloedDatabase__aaaaaaaa-0000-0000-0000-000000000001__ConnectionString</c>
/// </para>
/// <para>
/// Tenants without an explicit entry fall back to the shared default connection string so that
/// a deployment can migrate individual tenants to dedicated databases incrementally.
/// </para>
/// </remarks>
public sealed class SiloedDatabaseTenantResourcesProvider : ITenantResourcesProvider
{
    private const string SiloedKeyPrefix = "Tenancy:SiloedDatabase";
    private const string DefaultConnectionKey = "ConnectionStrings:DefaultConnection";

    private readonly IConfiguration _configuration;
    private readonly ILogger<SiloedDatabaseTenantResourcesProvider> _logger;

    /// <summary>Initializes the provider.</summary>
    public SiloedDatabaseTenantResourcesProvider(
        IConfiguration configuration,
        ILogger<SiloedDatabaseTenantResourcesProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        // Attempt per-tenant lookup first.
        string perTenantKey = $"{SiloedKeyPrefix}:{tenantId:D}:ConnectionString";
        string? connectionString = _configuration[perTenantKey];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogDebug(
                "Resolved per-tenant connection string for tenant {TenantId} from {Key}.",
                tenantId,
                perTenantKey);
            return new ValueTask<ITenantResources>(new SiloedDatabaseTenantResources(connectionString));
        }

        // Fall back to shared default connection string.
        string? defaultConnection = _configuration[DefaultConnectionKey];
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            _logger.LogWarning(
                "No per-tenant connection string found for tenant {TenantId} in SiloedDatabase mode. " +
                "Falling back to shared DefaultConnection. " +
                "Configure {Key} to isolate this tenant.",
                tenantId,
                perTenantKey);
            return new ValueTask<ITenantResources>(new SiloedDatabaseTenantResources(defaultConnection));
        }

        throw new InvalidOperationException(
            $"No connection string found for tenant {tenantId} in SiloedDatabase mode. " +
            $"Set {perTenantKey} or ConnectionStrings:DefaultConnection in configuration.");
    }
}
