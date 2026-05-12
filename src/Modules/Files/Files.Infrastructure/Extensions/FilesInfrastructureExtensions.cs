using Amazon;
using Amazon.S3;
using Files.Application.Abstractions;
using Files.Infrastructure.BlobStores;
using Files.Infrastructure.Imaging;
using Files.Infrastructure.Options;
using Files.Infrastructure.Quota;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Files.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Files module infrastructure services.
/// Provider is selected by <c>Files:Provider</c> config key:
/// <c>FileSystem | S3 | AzureBlob | Gcs</c>. Default is FileSystem (dev).
/// </summary>
public static class FilesInfrastructureExtensions
{
    /// <summary>Registers all infrastructure services for the Files module.</summary>
    public static IServiceCollection AddFilesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FilesOptions>(configuration.GetSection(FilesOptions.SectionName));

        string provider = configuration["Files:Provider"] ?? string.Empty;

        switch (provider.Trim().ToUpperInvariant())
        {
            case "S3":
                services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
                // M-O10 fix: register a long-lived IAmazonS3 singleton so S3BlobStore does not
                // allocate a new client per call (fixes per-call allocation and the disposed-client
                // stream bug in ReadAsync).
                services.AddSingleton<IAmazonS3>(sp =>
                {
                    S3Options s3Opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3Options>>().Value;
                    RegionEndpoint region = RegionEndpoint.GetBySystemName(s3Opts.Region);
                    return !string.IsNullOrWhiteSpace(s3Opts.AccessKeyId) && !string.IsNullOrWhiteSpace(s3Opts.SecretAccessKey)
                        ? new AmazonS3Client(s3Opts.AccessKeyId, s3Opts.SecretAccessKey, region)
                        : new AmazonS3Client(region);
                });
                services.AddScoped<IBlobStore, S3BlobStore>();
                break;

            case "AZUREBLOB":
                services.Configure<AzureBlobOptions>(configuration.GetSection(AzureBlobOptions.SectionName));
                services.AddScoped<IBlobStore, AzureBlobStore>();
                break;

            case "GCS":
                services.Configure<GcsOptions>(configuration.GetSection(GcsOptions.SectionName));
                services.AddScoped<IBlobStore, GcsBlobStore>();
                break;

            case "FILESYSTEM":
            default:
                if (!string.IsNullOrWhiteSpace(provider) &&
                    !provider.Equals("FileSystem", System.StringComparison.OrdinalIgnoreCase))
                {
                    // Unknown provider — warn and fall back to FileSystem.
                    services.AddScoped<IBlobStore>(sp =>
                    {
                        sp.GetRequiredService<ILogger<FileSystemBlobStore>>().LogWarning(
                            "Files module: unknown provider '{Provider}'. Falling back to FileSystem.",
                            provider);
                        return sp.GetRequiredService<FileSystemBlobStore>();
                    });
                    services.AddScoped<FileSystemBlobStore>();
                }
                else
                {
                    services.AddScoped<IBlobStore, FileSystemBlobStore>();
                }

                break;
        }

        // Image processor — always registered (no external service required at DI time).
        services.AddSingleton<IImageProcessor, ImageProcessor>();

        // Quota counter — in-memory for Phase 5 scaffold.
        services.AddSingleton<ITenantQuotaCounter, InMemoryTenantQuotaCounter>();

        return services;
    }
}
