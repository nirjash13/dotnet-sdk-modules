using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaasBuilder.Persistence;
using Search.Application.Abstractions;
using Search.Infrastructure.Adapters;
using Search.Infrastructure.Options;
using Search.Infrastructure.Persistence;

namespace Search.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Search module infrastructure services.
/// Provider is selected by <c>Search:Provider</c> config key:
/// <c>PostgresFts | OpenSearch | Meilisearch</c>. Default is PostgresFts.
/// </summary>
public static class SearchInfrastructureExtensions
{
    /// <summary>Registers the search client and supporting services.</summary>
    public static IServiceCollection AddSearchInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string provider = configuration["Search:Provider"] ?? string.Empty;

        switch (provider.Trim().ToUpperInvariant())
        {
            case "OPENSEARCH":
                services.Configure<OpenSearchOptions>(configuration.GetSection(OpenSearchOptions.SectionName));
                services.AddScoped<ISearchClient, OpenSearchAdapter>();
                break;

            case "MEILISEARCH":
                services.Configure<MeilisearchOptions>(configuration.GetSection(MeilisearchOptions.SectionName));
                services.AddScoped<ISearchClient, MeilisearchAdapter>();
                break;

            case "POSTGRESFTS":
            default:
                if (!string.IsNullOrWhiteSpace(provider) &&
                    !provider.Equals("PostgresFts", System.StringComparison.OrdinalIgnoreCase))
                {
                    services.AddScoped<ISearchClient>(sp =>
                    {
                        sp.GetRequiredService<ILogger<PostgresFullTextSearchClient>>().LogWarning(
                            "Search module: unknown provider '{Provider}'. Falling back to PostgresFts.",
                            provider);
                        return sp.GetRequiredService<PostgresFullTextSearchClient>();
                    });
                    RegisterPostgresFts(services, configuration);
                    services.AddScoped<PostgresFullTextSearchClient>();
                }
                else
                {
                    RegisterPostgresFts(services, configuration);
                }

                break;
        }

        return services;
    }

    private static void RegisterPostgresFts(IServiceCollection services, IConfiguration configuration)
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
    }
}
