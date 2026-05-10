using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SaasBuilder.SharedKernel.Tenancy;

namespace SaasBuilder.Persistence.Tenancy;

/// <summary>
/// <see cref="ITenantResourcesProvider"/> for the <c>PoolWithRls</c> isolation mode.
/// Returns the same shared <see cref="ITenantResources"/> record for every tenant —
/// the connection string is read once from <c>ConnectionStrings:DefaultConnection</c>
/// at construction time.
/// </summary>
/// <remarks>
/// "Pool" reflects the AWS SaaS Lens nomenclature where all tenants share the same database
/// cluster and are isolated by Row-Level Security policies rather than separate databases.
/// </remarks>
public sealed class PoolWithRlsTenantResourcesProvider : ITenantResourcesProvider
{
    private readonly PoolWithRlsTenantResources _sharedResources;

    /// <summary>
    /// Initializes the provider by reading the shared connection string from configuration.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>ConnectionStrings:DefaultConnection</c> is missing or empty.
    /// </exception>
    public PoolWithRlsTenantResourcesProvider(IConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required for PoolWithRls tenancy mode. " +
                "Add it to appsettings.json or set the CONNECTIONSTRINGS__DEFAULTCONNECTION environment variable.");
        }

        _sharedResources = new PoolWithRlsTenantResources(connectionString);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always returns the same shared resources record regardless of <paramref name="tenantId"/>.
    /// Tenant isolation is enforced by RLS on the database side, not by connection routing.
    /// </remarks>
    public ValueTask<ITenantResources> GetAsync(Guid tenantId, CancellationToken ct = default)
        => new ValueTask<ITenantResources>(_sharedResources);
}
