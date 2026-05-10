using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Files.Application.Abstractions;
using Files.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SaasBuilder.SharedKernel.Tenancy;

namespace Files.Infrastructure.BlobStores;

/// <summary>
/// Local filesystem blob store. Writes blobs to <c>FilesOptions.RootPath/{tenantId}/{key}</c>.
/// Presigned "URLs" are local HTTP routes: <c>/__local-blob/{tenantId}/{key}</c>.
/// Production deployments replace this with a cloud adapter (S3, Azure Blob, GCS, R2).
/// </summary>
internal sealed class FileSystemBlobStore(
    IOptions<FilesOptions> options,
    ITenantContextAccessor tenantAccessor,
    ILogger<FileSystemBlobStore> logger)
    : IBlobStore
{
    private readonly FilesOptions _opts = options.Value;

    /// <inheritdoc />
    public Task<Uri> PresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        Guid tenantId = RequireTenantId();
        // Local presigned URL is just a route to a local HTTP endpoint — no real signing.
        Uri url = new Uri($"http://localhost{_opts.LocalBlobRoutePrefix}/{tenantId}/{Uri.EscapeDataString(key)}?method=PUT");
        return Task.FromResult(url);
    }

    /// <inheritdoc />
    public Task<Uri> PresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        Guid tenantId = RequireTenantId();
        Uri url = new Uri($"http://localhost{_opts.LocalBlobRoutePrefix}/{tenantId}/{Uri.EscapeDataString(key)}");
        return Task.FromResult(url);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
    {
        string path = GetPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
        logger.LogDebug("FileSystemBlobStore: wrote {Key} ({Path})", key, path);
    }

    /// <inheritdoc />
    public Task<Stream> ReadAsync(string key, CancellationToken ct = default)
    {
        string path = GetPath(key);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Blob not found: {key}", path);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        string path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<long> GetSizeAsync(string key, CancellationToken ct = default)
    {
        string path = GetPath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult(-1L);
        }

        FileInfo info = new FileInfo(path);
        return Task.FromResult(info.Length);
    }

    private string GetPath(string key)
    {
        Guid tenantId = RequireTenantId();
        string safeKey = key.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_opts.RootPath, tenantId.ToString(), safeKey);
    }

    private Guid RequireTenantId()
    {
        Guid? id = tenantAccessor.Current?.TenantId;
        if (id is null)
        {
            throw new InvalidOperationException("No tenant context is established. Cannot determine blob storage scope.");
        }

        return id.Value;
    }
}
