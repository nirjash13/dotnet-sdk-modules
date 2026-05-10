using System;
using System.Threading;
using System.Threading.Tasks;
using Files.Application.Abstractions;
using Files.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Files.Api;

/// <summary><see cref="IModuleStartup"/> for the Files module.</summary>
public sealed class FilesModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddFilesInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/files")
            .WithTags("files")
            .RequireAuthorization();

        // POST /api/v1/files/upload-url
        group.MapPost("/upload-url", GetUploadUrlAsync)
            .WithName("Files_GetUploadUrl")
            .WithSummary("Returns a presigned PUT URL for direct client upload.");

        // POST /api/v1/files/download-url/{key}
        group.MapPost("/download-url/{*key}", GetDownloadUrlAsync)
            .WithName("Files_GetDownloadUrl")
            .WithSummary("Returns a presigned GET URL for direct client download.");

        // DELETE /api/v1/files/{key}
        group.MapDelete("/{*key}", DeleteAsync)
            .WithName("Files_Delete")
            .WithSummary("Deletes a blob.");
    }

    private static async Task<IResult> GetUploadUrlAsync(
        UploadUrlRequest request,
        IBlobStore blobStore,
        ITenantQuotaCounter quotaCounter,
        ITenantContextAccessor tenantAccessor,
        ILogger<FilesModule> logger,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        // Quota enforcement: check hard limit before allowing upload.
        long? hardLimit = await quotaCounter.GetHardLimitAsync(ctx.TenantId, ct).ConfigureAwait(false);
        if (hardLimit.HasValue)
        {
            long used = await quotaCounter.GetUsedBytesAsync(ctx.TenantId, ct).ConfigureAwait(false);
            if (used >= hardLimit.Value)
            {
                return Results.Problem(
                    title: "Storage quota exceeded.",
                    detail: $"Tenant {ctx.TenantId} has reached its storage limit of {hardLimit.Value} bytes.",
                    statusCode: 413);
            }

            long? softLimit = await quotaCounter.GetSoftLimitAsync(ctx.TenantId, ct).ConfigureAwait(false);
            if (softLimit.HasValue && used >= softLimit.Value)
            {
                logger.LogWarning(
                    "Files: tenant {TenantId} is approaching storage quota ({Used}/{Hard} bytes).",
                    ctx.TenantId, used, hardLimit.Value);
            }
        }

        Uri url = await blobStore.PresignedUploadUrlAsync(
            request.Key,
            request.ContentType ?? "application/octet-stream",
            TimeSpan.FromMinutes(15),
            ct).ConfigureAwait(false);

        return Results.Ok(new { Url = url.ToString(), ExpiresInSeconds = 900 });
    }

    private static async Task<IResult> GetDownloadUrlAsync(
        string key,
        IBlobStore blobStore,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        if (tenantAccessor.Current is null)
        {
            return Results.Unauthorized();
        }

        Uri url = await blobStore.PresignedDownloadUrlAsync(key, TimeSpan.FromMinutes(15), ct)
            .ConfigureAwait(false);

        return Results.Ok(new { Url = url.ToString(), ExpiresInSeconds = 900 });
    }

    private static async Task<IResult> DeleteAsync(
        string key,
        IBlobStore blobStore,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        if (tenantAccessor.Current is null)
        {
            return Results.Unauthorized();
        }

        await blobStore.DeleteAsync(key, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    /// <summary>Request body for <c>POST /upload-url</c>.</summary>
    public sealed class UploadUrlRequest
    {
        /// <summary>Gets or sets the blob key (path within the tenant's storage scope).</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Gets or sets the MIME content type of the blob being uploaded.</summary>
        public string? ContentType { get; set; }
    }
}
