using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Persistence;
using Search.Application.Abstractions;
using Search.Infrastructure.Persistence;

namespace Search.Infrastructure.Extensions;

/// <summary>Extension methods for registering Search module infrastructure services.</summary>
public static class SearchInfrastructureExtensions
{
    /// <summary>
    /// Registers the Postgres FTS search client and supporting services.
    /// Implements silent degradation: if no connection string is configured, search operations
    /// are no-ops (no exception at startup).
    /// </summary>
    public static IServiceCollection AddSearchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (connectionString is not null)
        {
            services.AddSaasBuilderPersistence<SearchDbContext>(options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(SearchDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "search");
                    });
            });

            services.AddScoped<ISearchClient, PostgresFullTextSearchClient>();
        }

        return services;
    }
}
