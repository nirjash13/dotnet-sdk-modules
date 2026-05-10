using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Abstractions;
using Reporting.Infrastructure.Persistence;
using SaasBuilder.Persistence;

namespace Reporting.Infrastructure.Extensions;

/// <summary>
/// Extension methods that wire up the Reporting module's infrastructure services:
/// EF Core <see cref="ReportingDbContext"/> and the <see cref="IReportingDbContext"/> abstraction.
/// </summary>
public static class ReportingInfrastructureExtensions
{
    /// <summary>
    /// Registers all infrastructure services required by the Reporting module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddReportingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'SaasBuilder' (or fallback 'DefaultConnection') is not configured. " +
                "Set it via environment variable 'ConnectionStrings__SaasBuilder'.");

        // EF Core DbContext via SaasBuilder persistence helper (adds interceptor + accessor).
        services.AddSaasBuilderPersistence<ReportingDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ReportingDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "reporting");
                });
        });

        // Register the DbContext as IReportingDbContext so Application consumers resolve it
        // without a direct EF Core dependency.
        services.AddScoped<IReportingDbContext>(
            sp => sp.GetRequiredService<ReportingDbContext>());

        return services;
    }
}
