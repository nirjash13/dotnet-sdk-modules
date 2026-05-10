using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Files.Application.Abstractions;

namespace Files.Infrastructure.BlobStores;

// ---------------------------------------------------------------------------
// Cloud blob store stubs — Phase 5.2
// ---------------------------------------------------------------------------

/// <summary>TODO(Phase 5.2): Azure Blob Storage adapter — install Azure.Storage.Blobs NuGet package.</summary>
internal sealed class AzureBlobStore : IBlobStore
{
    public Task<Uri> PresignedUploadUrlAsync(string key, string contentType, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Azure Blob Storage integration");

    public Task<Uri> PresignedDownloadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Azure Blob Storage integration");

    public Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Azure Blob Storage integration");

    public Task<Stream> ReadAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Azure Blob Storage integration");

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Azure Blob Storage integration");

    public Task<long> GetSizeAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Azure Blob Storage integration");
}

/// <summary>TODO(Phase 5.2): AWS S3 adapter — install AWSSDK.S3 NuGet package.</summary>
internal sealed class S3BlobStore : IBlobStore
{
    public Task<Uri> PresignedUploadUrlAsync(string key, string contentType, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): S3 integration");

    public Task<Uri> PresignedDownloadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): S3 integration");

    public Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): S3 integration");

    public Task<Stream> ReadAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): S3 integration");

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): S3 integration");

    public Task<long> GetSizeAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): S3 integration");
}

/// <summary>TODO(Phase 5.2): Google Cloud Storage adapter — install Google.Cloud.Storage.V1 NuGet package.</summary>
internal sealed class GcsBlobStore : IBlobStore
{
    public Task<Uri> PresignedUploadUrlAsync(string key, string contentType, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): GCS integration");

    public Task<Uri> PresignedDownloadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): GCS integration");

    public Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): GCS integration");

    public Task<Stream> ReadAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): GCS integration");

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): GCS integration");

    public Task<long> GetSizeAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): GCS integration");
}

/// <summary>TODO(Phase 5.2): Cloudflare R2 adapter — install Cloudflare R2 or S3-compat NuGet package.</summary>
internal sealed class R2BlobStore : IBlobStore
{
    public Task<Uri> PresignedUploadUrlAsync(string key, string contentType, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Cloudflare R2 integration");

    public Task<Uri> PresignedDownloadUrlAsync(string key, TimeSpan expiresIn, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Cloudflare R2 integration");

    public Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Cloudflare R2 integration");

    public Task<Stream> ReadAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Cloudflare R2 integration");

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Cloudflare R2 integration");

    public Task<long> GetSizeAsync(string key, CancellationToken ct = default)
        => throw new NotImplementedException("TODO(Phase 5.2): Cloudflare R2 integration");
}
