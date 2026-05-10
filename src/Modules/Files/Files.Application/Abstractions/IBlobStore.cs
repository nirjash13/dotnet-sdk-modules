using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Files.Application.Abstractions;

/// <summary>
/// Tenant-scoped blob storage abstraction.
/// All operations are automatically scoped to the current tenant's container/prefix
/// via <c>ITenantResources.BlobContainer</c>.
/// </summary>
public interface IBlobStore
{
    /// <summary>
    /// Generates a presigned URL that allows a client to upload a blob directly.
    /// </summary>
    /// <param name="key">The blob key (path within the tenant scope).</param>
    /// <param name="contentType">Expected MIME type of the upload.</param>
    /// <param name="expiresIn">How long the URL is valid.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A presigned PUT URL.</returns>
    Task<Uri> PresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a presigned URL that allows a client to download a blob directly.
    /// </summary>
    Task<Uri> PresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken ct = default);

    /// <summary>Writes <paramref name="stream"/> as a blob at <paramref name="key"/>.</summary>
    Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default);

    /// <summary>Opens a read stream for the blob at <paramref name="key"/>.</summary>
    Task<Stream> ReadAsync(string key, CancellationToken ct = default);

    /// <summary>Deletes the blob at <paramref name="key"/>. No-ops if it does not exist.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>Returns the size of the blob in bytes, or -1 if it does not exist.</summary>
    Task<long> GetSizeAsync(string key, CancellationToken ct = default);
}
