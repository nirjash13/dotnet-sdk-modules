using System;
using Entitlements.Application;
using Entitlements.Application.Abstractions;
using Entitlements.Application.Authorization;
using Entitlements.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Persistence;

namespace Entitlements.Infrastructure.Extensions;

/// <summary>
/// Extension methods that wire up the Entitlements module's infrastructure services.
/// </summary>
public static class EntitlementsInfrastructureExtensions
{
    /// <summary>
    /// Registers the Entitlements module infrastructure into the DI container.
    /// Call this from the host or from <see cref="EntitlementsModuleStartup"/>.
    /// </summary>
    public static IServiceCollection AddEntitlementsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'SaasBuilder' (or fallback 'DefaultConnection') is not configured.");

        // 1. EF Core DbContext.
        services.AddSaasBuilderPersistence<EntitlementsDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(EntitlementsDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "entitlements");
                });
        });

        // 2. Repository.
        services.AddScoped<IEntitlementRepository, EntitlementRepository>();

        // 3. IMemoryCache — required by EntitlementService for 5-min TTL caching.
        // AddMemoryCache is idempotent — safe to call multiple times.
        services.AddMemoryCache();

        // Application service. Singleton so the IMemoryCache is shared within a process.
        services.AddSingleton<EntitlementService>();
        services.AddSingleton<IEntitlementService>(sp => sp.GetRequiredService<EntitlementService>());

        // 4. ASP.NET Core authorization handler.
        services.AddScoped<IAuthorizationHandler, RequiresEntitlementAuthorizationHandler>();

        return services;
    }
}
