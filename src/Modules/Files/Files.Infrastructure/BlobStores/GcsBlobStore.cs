using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Files.Application.Abstractions;
using Files.Infrastructure.Options;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using SaasBuilder.SharedKernel.Tenancy;

namespace Files.Infrastructure.BlobStores;

/// <summary>
/// Google Cloud Storage blob store. Each blob is stored at <c>{tenantId}/{key}</c>
/// within the configured bucket. Presigned (signed) URLs use service account credentials.
/// </summary>
internal sealed class GcsBlobStore(
    IOptions<GcsOptions> options,
    ITenantContextAccessor tenantAccessor)
    : IBlobStore
{
    private readonly GcsOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task<Uri> PresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        UrlSigner signer = await BuildUrlSignerAsync().ConfigureAwait(false);

        UrlSigner.RequestTemplate template = UrlSigner.RequestTemplate
            .FromBucket(_opts.BucketName)
            .WithObjectName(TenantKey(key))
            .WithHttpMethod(System.Net.Http.HttpMethod.Put)
            .WithContentHeaders(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.IEnumerable<string>>(
                    "Content-Type", new[] { contentType }),
            });

        string url = await signer.SignAsync(template, UrlSigner.Options.FromDuration(expiresIn), ct)
            .ConfigureAwait(false);
        return new Uri(url);
    }

    /// <inheritdoc />
    public async Task<Uri> PresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        UrlSigner signer = await BuildUrlSignerAsync().ConfigureAwait(false);

        UrlSigner.RequestTemplate template = UrlSigner.RequestTemplate
            .FromBucket(_opts.BucketName)
            .WithObjectName(TenantKey(key))
            .WithHttpMethod(System.Net.Http.HttpMethod.Get);

        string url = await signer.SignAsync(template, UrlSigner.Options.FromDuration(expiresIn), ct)
            .ConfigureAwait(false);
        return new Uri(url);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
    {
        StorageClient client = await StorageClient.CreateAsync().ConfigureAwait(false);
        await client.UploadObjectAsync(
            _opts.BucketName,
            TenantKey(key),
            contentType ?? "application/octet-stream",
            stream,
            cancellationToken: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> ReadAsync(string key, CancellationToken ct = default)
    {
        StorageClient client = await StorageClient.CreateAsync().ConfigureAwait(false);
        MemoryStream ms = new MemoryStream();
        await client.DownloadObjectAsync(_opts.BucketName, TenantKey(key), ms, cancellationToken: ct)
            .ConfigureAwait(false);
        ms.Position = 0;
        return ms;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        StorageClient client = await StorageClient.CreateAsync().ConfigureAwait(false);
        await client.DeleteObjectAsync(_opts.BucketName, TenantKey(key), cancellationToken: ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            StorageClient client = await StorageClient.CreateAsync().ConfigureAwait(false);
            Google.Apis.Storage.v1.Data.Object obj =
                await client.GetObjectAsync(_opts.BucketName, TenantKey(key), cancellationToken: ct)
                    .ConfigureAwait(false);
            return (long)(obj.Size ?? 0UL);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return -1L;
        }
    }

    private string TenantKey(string key)
    {
        Guid tenantId = tenantAccessor.Current?.TenantId
            ?? throw new InvalidOperationException("No tenant context established for GCS blob operation.");
        return $"{tenantId}/{key}";
    }

    private async Task<UrlSigner> BuildUrlSignerAsync()
    {
        if (!string.IsNullOrWhiteSpace(_opts.CredentialFile))
        {
            GoogleCredential credential = await GoogleCredential.FromFileAsync(_opts.CredentialFile, CancellationToken.None)
                .ConfigureAwait(false);
            return UrlSigner.FromCredential(credential);
        }

        GoogleCredential defaultCredential = await GoogleCredential.GetApplicationDefaultAsync().ConfigureAwait(false);
        return UrlSigner.FromCredential(defaultCredential);
    }
}
