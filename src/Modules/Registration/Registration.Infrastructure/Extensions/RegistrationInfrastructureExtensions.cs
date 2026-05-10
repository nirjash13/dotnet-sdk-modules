using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Registration.Infrastructure.Persistence;
using SaasBuilder.Persistence;

namespace Registration.Infrastructure.Extensions;

/// <summary>
/// Extension methods that wire up the Registration module's infrastructure services:
/// EF Core <see cref="RegistrationDbContext"/> for saga state persistence.
/// </summary>
public static class RegistrationInfrastructureExtensions
{
    /// <summary>
    /// Registers all infrastructure services required by the Registration module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddRegistrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'SaasBuilder' (or fallback 'DefaultConnection') is not configured. " +
                "Set it via environment variable 'ConnectionStrings__SaasBuilder'.");

        // EF Core DbContext via SaasBuilder persistence helper (adds TenantCommandInterceptor + accessor).
        services.AddSaasBuilderPersistence<RegistrationDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(RegistrationDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "registration");
                });
        });

        return services;
    }
}
