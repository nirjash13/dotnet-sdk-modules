using Files.Application.Abstractions;
using Files.Infrastructure.BlobStores;
using Files.Infrastructure.Options;
using Files.Infrastructure.Quota;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Files.Infrastructure.Extensions;

/// <summary>Extension methods for registering Files module infrastructure services.</summary>
public static class FilesInfrastructureExtensions
{
    /// <summary>Registers all infrastructure services for the Files module.</summary>
    public static IServiceCollection AddFilesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FilesOptions>(configuration.GetSection(FilesOptions.SectionName));

        // Default blob store — local filesystem. Override by calling AddScoped<IBlobStore, CloudStore>
        // in the host or via a module option in a later phase.
        services.AddScoped<IBlobStore, FileSystemBlobStore>();

        // Quota counter — in-memory for Phase 5 scaffold.
        // TODO(Phase 5.2): swap for EfCoreTenantQuotaCounter when DB-backed persistence is required.
        services.AddSingleton<ITenantQuotaCounter, InMemoryTenantQuotaCounter>();

        return services;
    }
}
