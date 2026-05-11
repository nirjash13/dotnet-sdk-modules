using FluentValidation;
using Gdpr.Application;
using Gdpr.Application.Abstractions;
using Gdpr.Application.Validators;
using Gdpr.Infrastructure.Data;
using Gdpr.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gdpr.Infrastructure.Extensions;

/// <summary>Registers GDPR module infrastructure services.</summary>
public static class GdprInfrastructureExtensions
{
    /// <summary>
    /// Adds GDPR module services: DbContext, repositories, export builder, and erasure worker.
    /// </summary>
    public static IServiceCollection AddGdprInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("GdprDb")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=saasbuilder";

        services.AddDbContext<GdprDbContext>(opts =>
            opts.UseNpgsql(connectionString));

        services.AddScoped<IGdprConsentRepository, GdprConsentRepository>();
        services.AddScoped<IGdprErasureRepository, GdprErasureRepository>();
        services.AddScoped<IGdprSubProcessorRepository, GdprSubProcessorRepository>();

        services.AddScoped<IDataExportBuilder, DataExportBuilder>();

        services.AddHostedService<ErasureWorker>();

        services.AddValidatorsFromAssemblyContaining<RecordConsentCommandValidator>(
            lifetime: ServiceLifetime.Scoped);

        return services;
    }
}
