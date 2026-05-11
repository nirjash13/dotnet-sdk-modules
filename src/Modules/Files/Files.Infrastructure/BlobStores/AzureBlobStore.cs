using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Files.Application.Abstractions;
using Files.Infrastructure.Options;
using Microsoft.Extensions.Options;
using SaasBuilder.SharedKernel.Tenancy;

namespace Files.Infrastructure.BlobStores;

/// <summary>
/// Azure Blob Storage blob store. Each tenant gets container <c>{prefix}-{tenantId:N}</c>.
/// Presigned (SAS) URLs are generated for direct client upload/download.
/// </summary>
internal sealed class AzureBlobStore(
    IOptions<AzureBlobOptions> options,
    ITenantContextAccessor tenantAccessor)
    : IBlobStore
{
    private readonly AzureBlobOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task<Uri> PresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        (BlobContainerClient container, BlobClient blob) = GetClients(key);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct)
            .ConfigureAwait(false);

        Uri sasUri = blob.GenerateSasUri(
            BlobSasPermissions.Write | BlobSasPermissions.Create,
            DateTimeOffset.UtcNow.Add(expiresIn));
        return sasUri;
    }

    /// <inheritdoc />
    public Task<Uri> PresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        (_, BlobClient blob) = GetClients(key);
        Uri sasUri = blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiresIn));
        return Task.FromResult(sasUri);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
    {
        (BlobContainerClient container, BlobClient blob) = GetClients(key);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct)
            .ConfigureAwait(false);

        BlobUploadOptions uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType ?? "application/octet-stream" },
        };

        await blob.UploadAsync(stream, uploadOptions, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> ReadAsync(string key, CancellationToken ct = default)
    {
        (_, BlobClient blob) = GetClients(key);
        Azure.Response<Azure.Storage.Blobs.Models.BlobDownloadResult> download =
            await blob.DownloadContentAsync(ct).ConfigureAwait(false);
        return download.Value.Content.ToStream();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        (_, BlobClient blob) = GetClients(key);
        await blob.DeleteIfExistsAsync(cancellationToken: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(string key, CancellationToken ct = default)
    {
        (_, BlobClient blob) = GetClients(key);
        if (!await blob.ExistsAsync(ct).ConfigureAwait(false))
        {
            return -1L;
        }

        BlobProperties props = await blob.GetPropertiesAsync(cancellationToken: ct).ConfigureAwait(false);
        return props.ContentLength;
    }

    private (BlobContainerClient Container, BlobClient Blob) GetClients(string key)
    {
        Guid tenantId = tenantAccessor.Current?.TenantId
            ?? throw new InvalidOperationException("No tenant context established for Azure Blob operation.");

        string containerName = $"{_opts.ContainerPrefix}-{tenantId:N}".ToLowerInvariant();
        BlobContainerClient container = new BlobContainerClient(_opts.ConnectionString, containerName);
        BlobClient blob = container.GetBlobClient(key);
        return (container, blob);
    }
}
