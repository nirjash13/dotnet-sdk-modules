using Hangfire;
using Hangfire.PostgreSql;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.DeadLetter;
using Jobs.Infrastructure.Options;
using Jobs.Infrastructure.Scheduler;
using Jobs.Infrastructure.Schedulers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobs.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Jobs module infrastructure services.
/// Provider is selected by <c>Jobs:Provider</c> config key:
/// <c>InMemory | Hangfire</c>. Default is InMemory.
/// </summary>
public static class JobsInfrastructureExtensions
{
    /// <summary>Registers all infrastructure services for the Jobs module.</summary>
    public static IServiceCollection AddJobsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string provider = configuration["Jobs:Provider"] ?? string.Empty;

        switch (provider.Trim().ToUpperInvariant())
        {
            case "HANGFIRE":
                RegisterHangfire(services, configuration);
                break;

            default:
                if (!string.IsNullOrWhiteSpace(provider) &&
                    !provider.Equals("InMemory", System.StringComparison.OrdinalIgnoreCase))
                {
                    services.AddSingleton<InProcessJobScheduler>();
                    services.AddSingleton<IJobScheduler>(sp =>
                    {
                        sp.GetRequiredService<ILogger<InProcessJobScheduler>>().LogWarning(
                            "Jobs module: unknown provider '{Provider}'. Falling back to InMemory.",
                            provider);
                        return sp.GetRequiredService<InProcessJobScheduler>();
                    });
                }
                else
                {
                    services.AddSingleton<InProcessJobScheduler>();
                    services.AddSingleton<IJobScheduler>(sp => sp.GetRequiredService<InProcessJobScheduler>());
                }

                services.AddHostedService<InProcessJobWorker>();
                break;
        }

        // DLQ store — in-memory for Phase 5.
        services.AddSingleton<IDeadLetterQueueStore, InMemoryDeadLetterQueueStore>();

        return services;
    }

    private static void RegisterHangfire(IServiceCollection services, IConfiguration configuration)
    {
        HangfireOptions opts = new HangfireOptions();
        configuration.GetSection(HangfireOptions.SectionName).Bind(opts);
        services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));

        string connectionString = opts.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Fall back to the shared connection string.
            connectionString = configuration.GetConnectionString("SaasBuilder")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddHangfire(config =>
            {
                config.UseSimpleAssemblyNameTypeSerializer();
                config.UseRecommendedSerializerSettings();
                config.UsePostgreSqlStorage(
                    pgopts =>
                    {
                        pgopts.UseNpgsqlConnection(connectionString);
                    },
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = opts.SchemaName,
                    });
            });

            services.AddHangfireServer(opts2 =>
            {
                opts2.WorkerCount = opts.WorkerCount;
            });
        }

        services.AddScoped<HangfireJobDispatcher>();
        services.AddScoped<IJobScheduler, HangfireJobScheduler>();

        // Singleton allowlist registry for type-safe deserialization (C-4).
        // Job types are registered at startup via IJobTypeRegistry.Register<T>().
        services.AddSingleton<IJobTypeRegistry, JobTypeRegistry>();
    }
}
