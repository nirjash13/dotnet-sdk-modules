using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Files.Application.Abstractions;
using Files.Infrastructure.Options;
using Microsoft.Extensions.Options;
using SaasBuilder.SharedKernel.Tenancy;

namespace Files.Infrastructure.BlobStores;

/// <summary>
/// AWS S3 blob store. Each blob is stored at <c>{tenantId}/{key}</c> within the configured bucket.
/// Presigned URLs are generated using the S3 GetPreSignedURL API.
/// </summary>
/// <remarks>
/// M-O10 fix: <see cref="IAmazonS3"/> is injected as a long-lived singleton from DI rather than
/// being allocated per-call. This also eliminates the disposed-client bug in <see cref="ReadAsync"/>
/// where the stream was returned after the <c>using</c> block disposed the client.
/// </remarks>
internal sealed class S3BlobStore(
    IAmazonS3 s3Client,
    IOptions<S3Options> options,
    ITenantContextAccessor tenantAccessor)
    : IBlobStore
{
    private readonly S3Options _opts = options.Value;

    /// <inheritdoc />
    public Task<Uri> PresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        // TODO(M-O11): S3 PUT presigned URLs cannot enforce a maximum content-length at signing time —
        // the SDK's GetPreSignedUrlRequest has no size-policy parameter for PUT.
        // To enforce size limits server-side, switch to presigned POST (CreatePresignedPost) which
        // supports a content-length-range policy condition. Until then, file-size enforcement is
        // performed at the API layer (allowlist + size check in FilesModule) before issuing this URL.
        GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiresIn),
        };

        string url = s3Client.GetPreSignedURL(request);
        return Task.FromResult(new Uri(url));
    }

    /// <inheritdoc />
    public Task<Uri> PresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn),
        };

        string url = s3Client.GetPreSignedURL(request);
        return Task.FromResult(new Uri(url));
    }

    /// <inheritdoc />
    public async Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
    {
        PutObjectRequest request = new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
            InputStream = stream,
            ContentType = contentType ?? "application/octet-stream",
            AutoCloseStream = false,
        };

        await s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> ReadAsync(string key, CancellationToken ct = default)
    {
        // M-O10 fix: s3Client is a DI singleton — not disposed here, so the returned
        // ResponseStream remains valid for the caller to read and dispose.
        GetObjectRequest request = new GetObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
        };

        GetObjectResponse response = await s3Client.GetObjectAsync(request, ct).ConfigureAwait(false);
        return response.ResponseStream;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        DeleteObjectRequest request = new DeleteObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
        };

        await s3Client.DeleteObjectAsync(request, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            GetObjectMetadataRequest request = new GetObjectMetadataRequest
            {
                BucketName = _opts.BucketName,
                Key = TenantKey(key),
            };

            GetObjectMetadataResponse response = await s3Client.GetObjectMetadataAsync(request, ct)
                .ConfigureAwait(false);
            return response.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return -1L;
        }
    }

    private string TenantKey(string key)
    {
        Guid tenantId = tenantAccessor.Current?.TenantId
            ?? throw new InvalidOperationException("No tenant context established for S3 blob operation.");
        return $"{tenantId}/{key}";
    }
}
