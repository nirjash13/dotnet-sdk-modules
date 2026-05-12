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
/// Throws <see cref="InvalidOperationException"/> when no per-tenant entry is found —
/// there is no shared-pool fallback in this mode.
/// </summary>
/// <remarks>
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
/// </remarks>
public sealed class SiloedDatabaseTenantResourcesProvider : ITenantResourcesProvider
{
    private const string SiloedKeyPrefix = "Tenancy:SiloedDatabase";

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

        // In SiloedDatabase mode every tenant MUST have an explicit connection string.
        // Falling back to a shared pool would silently break the isolation guarantee and
        // expose all tenants to each other — throw instead so the misconfiguration is
        // discovered at startup / first request rather than at data-breach review time.
        throw new InvalidOperationException(
            $"SiloedDatabase: no per-tenant connection string found for tenant {tenantId}. " +
            $"Set configuration key '{perTenantKey}'. " +
            "Fallback to a shared pool is intentionally disabled in SiloedDatabase isolation mode.");
    }
}
