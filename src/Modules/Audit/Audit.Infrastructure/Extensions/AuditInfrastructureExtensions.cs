using Audit.Application.Abstractions;
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

        return services;
    }
}
