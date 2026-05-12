using Audit.Application.Abstractions;
using Audit.Infrastructure.Forwarders;
using Audit.Infrastructure.Loggers;
using Audit.Infrastructure.Options;
using Audit.Infrastructure.Persistence;
using Audit.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Persistence;

namespace Audit.Infrastructure.Extensions;

/// <summary>Extension methods for registering Audit module infrastructure services.</summary>
public static class AuditInfrastructureExtensions
{
    /// <summary>Registers all infrastructure services for the Audit module.</summary>
    public static IServiceCollection AddAuditInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AuditOptions opts = new AuditOptions();
        configuration.GetSection(AuditOptions.SectionName).Bind(opts);
        services.Configure<AuditOptions>(configuration.GetSection(AuditOptions.SectionName));

        string? connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (connectionString is not null)
        {
            services.AddSaasBuilderPersistence<AuditDbContext>(options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit");
                    });
            });

            if (opts.EnableHashChain)
            {
                services.AddScoped<IAuditLogger, HashChainedAuditLogger>();
            }
            else
            {
                services.AddScoped<IAuditLogger, EfCoreAuditLogger>();
            }

            services.AddScoped<IAuditEventQuery, EfCoreAuditEventQuery>();
        }
        else
        {
            // Silent degradation: no DB configured, use no-op logger.
            services.AddScoped<IAuditLogger, NoOpAuditLogger>();
        }

        // Register SIEM forwarders if configured — they are IHostedService singletons with background queues.
        SplunkHecOptions splunkOpts = new SplunkHecOptions();
        configuration.GetSection(SplunkHecOptions.SectionName).Bind(splunkOpts);

        if (!string.IsNullOrWhiteSpace(splunkOpts.Url) && !string.IsNullOrWhiteSpace(splunkOpts.Token))
        {
            services.Configure<SplunkHecOptions>(configuration.GetSection(SplunkHecOptions.SectionName));
            services.AddSingleton<SplunkHecForwarder>();
            services.AddHostedService(sp => sp.GetRequiredService<SplunkHecForwarder>());

            // M-O2 fix: also expose as IAuditLogger so the audit pipeline can deliver events.
            services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<SplunkHecForwarder>());
        }

        DatadogForwarderOptions datadogOpts = new DatadogForwarderOptions();
        configuration.GetSection(DatadogForwarderOptions.SectionName).Bind(datadogOpts);

        if (!string.IsNullOrWhiteSpace(datadogOpts.ApiKey))
        {
            services.Configure<DatadogForwarderOptions>(configuration.GetSection(DatadogForwarderOptions.SectionName));
            services.AddSingleton<DatadogForwarder>();
            services.AddHostedService(sp => sp.GetRequiredService<DatadogForwarder>());

            // M-O2 fix: also expose as IAuditLogger so the audit pipeline can deliver events.
            services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<DatadogForwarder>());
        }

        // HttpClient for SIEM forwarders.
        services.AddHttpClient("splunk-hec");
        services.AddHttpClient("datadog-logs");

        return services;
    }
}
