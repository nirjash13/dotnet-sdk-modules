using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// <see cref="ITenantResourcesProvider"/> for the <c>SiloedSchema</c> isolation mode.
/// All tenants share the same connection string; each tenant uses a dedicated Postgres schema.
/// The schema name follows the convention <c>tenant_{tenantId:N}</c> (no hyphens, lower-case).
/// Callers can override the schema name per-tenant via:
/// <c>Tenancy:SiloedSchema:{tenantId}:Schema</c> in configuration.
/// </summary>
public sealed class SiloedSchemaTenantResourcesProvider : ITenantResourcesProvider
{
    private const string SiloedKeyPrefix = "Tenancy:SiloedSchema";
    private const string DefaultConnectionKey = "ConnectionStrings:DefaultConnection";

    private readonly IConfiguration _configuration;
    private readonly ILogger<SiloedSchemaTenantResourcesProvider> _logger;

    /// <summary>Initializes the provider.</summary>
    public SiloedSchemaTenantResourcesProvider(
        IConfiguration configuration,
        ILogger<SiloedSchemaTenantResourcesProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        string? connectionString = _configuration[DefaultConnectionKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required for SiloedSchema tenancy mode.");
        }

        // Per-tenant schema name override; fallback to convention.
        string conventionalSchema = $"tenant_{tenantId:N}";
        string schemaKey = $"{SiloedKeyPrefix}:{tenantId:D}:Schema";
        string schemaName = _configuration[schemaKey] ?? conventionalSchema;

        _logger.LogDebug(
            "Resolved SiloedSchema resources for tenant {TenantId}: schema={Schema}.",
            tenantId,
            schemaName);

        return new ValueTask<ITenantResources>(
            new SiloedSchemaTenantResources(connectionString, schemaName));
    }
}
