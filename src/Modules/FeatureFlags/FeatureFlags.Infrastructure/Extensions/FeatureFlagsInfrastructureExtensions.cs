using System;
using FeatureFlags.Application;
using FeatureFlags.Application.Abstractions;
using FeatureFlags.Infrastructure.Persistence;
using FeatureFlags.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Persistence;

namespace FeatureFlags.Infrastructure.Extensions;

/// <summary>
/// Extension methods that wire up the FeatureFlags module's infrastructure services.
/// </summary>
public static class FeatureFlagsInfrastructureExtensions
{
    /// <summary>
    /// Registers the FeatureFlags module's infrastructure services.
    /// </summary>
    public static IServiceCollection AddFeatureFlagsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'SaasBuilder' (or fallback 'DefaultConnection') is not configured.");

        // 1. EF Core DbContext.
        services.AddSaasBuilderPersistence<FeatureFlagsDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(FeatureFlagsDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "feature_flags");
                });
        });

        // 2. Default DB-backed provider. Override by registering a different IFeatureProvider.
        services.AddScoped<IFeatureProvider, DatabaseFeatureProvider>();

        // 3. Feature client (singleton is fine — uses scoped provider via DI scope).
        // Use scoped since it depends on scoped services (tenant accessor resolves per-request).
        services.AddScoped<IFeatureClient, FeatureClient>();

        return services;
    }
}
