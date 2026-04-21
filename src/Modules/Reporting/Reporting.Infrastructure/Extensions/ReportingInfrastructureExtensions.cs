using System;
using Chassis.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Abstractions;
using Reporting.Infrastructure.Persistence;

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
        string connectionString = configuration.GetConnectionString("Chassis")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'Chassis' (or fallback 'DefaultConnection') is not configured. " +
                "Set it via environment variable 'ConnectionStrings__Chassis'.");

        // EF Core DbContext via Chassis persistence helper (adds interceptor + accessor).
        services.AddChassisPersistence<ReportingDbContext>(options =>
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
