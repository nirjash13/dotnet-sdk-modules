using Marketplace.Application.Abstractions;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Infrastructure.Extensions;

/// <summary>Registers all Marketplace module infrastructure services.</summary>
public static class MarketplaceInfrastructureExtensions
{
    /// <summary>Adds Marketplace infrastructure to the service collection.</summary>
    public static IServiceCollection AddMarketplaceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("MarketplaceDb")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<MarketplaceDbContext>(options =>
            options.UseNpgsql(connectionString ?? "Host=localhost;Database=saasbuilder_marketplace"));

        services.AddScoped<IMarketplaceAppRegistry, EfMarketplaceAppRegistry>();
        services.AddScoped<IAppInstallationService, EfAppInstallationService>();

        return services;
    }
}
