using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Persistence;
using Webhooks.Application.Abstractions;
using Webhooks.Infrastructure.Persistence;
using Webhooks.Infrastructure.Queries;
using Webhooks.Infrastructure.Repositories;
using Webhooks.Infrastructure.Sender;

namespace Webhooks.Infrastructure.Extensions;

/// <summary>Extension methods for registering Webhooks module infrastructure services.</summary>
public static class WebhooksInfrastructureExtensions
{
    /// <summary>Registers all infrastructure services for the Webhooks module.</summary>
    public static IServiceCollection AddWebhooksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (connectionString is not null)
        {
            services.AddSaasBuilderPersistence<WebhooksDbContext>(options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(WebhooksDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "webhooks");
                    });
            });
        }

        services.AddScoped<IWebhookEndpointRepository, EfCoreWebhookEndpointRepository>();
        services.AddScoped<IWebhookSender, HttpWebhookSender>();
        services.AddScoped<IWebhookDeliveryQuery, EfCoreWebhookDeliveryQuery>();

        // Named HttpClient for webhook delivery — configure timeout and retry via Polly separately.
        services.AddHttpClient("webhooks")
            .ConfigureHttpClient(c => c.Timeout = System.TimeSpan.FromSeconds(30));

        return services;
    }
}
