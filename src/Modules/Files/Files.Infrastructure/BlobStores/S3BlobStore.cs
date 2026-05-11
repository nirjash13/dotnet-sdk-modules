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
internal sealed class S3BlobStore(
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
        using AmazonS3Client client = BuildClient();
        GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiresIn),
        };

        string url = client.GetPreSignedURL(request);
        return Task.FromResult(new Uri(url));
    }

    /// <inheritdoc />
    public Task<Uri> PresignedDownloadUrlAsync(
        string key,
        TimeSpan expiresIn,
        CancellationToken ct = default)
    {
        using AmazonS3Client client = BuildClient();
        GetPreSignedUrlRequest request = new GetPreSignedUrlRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn),
        };

        string url = client.GetPreSignedURL(request);
        return Task.FromResult(new Uri(url));
    }

    /// <inheritdoc />
    public async Task WriteAsync(string key, Stream stream, string? contentType, CancellationToken ct = default)
    {
        using AmazonS3Client client = BuildClient();
        PutObjectRequest request = new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
            InputStream = stream,
            ContentType = contentType ?? "application/octet-stream",
            AutoCloseStream = false,
        };

        await client.PutObjectAsync(request, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> ReadAsync(string key, CancellationToken ct = default)
    {
        using AmazonS3Client client = BuildClient();
        GetObjectRequest request = new GetObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
        };

        GetObjectResponse response = await client.GetObjectAsync(request, ct).ConfigureAwait(false);
        return response.ResponseStream;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        using AmazonS3Client client = BuildClient();
        DeleteObjectRequest request = new DeleteObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = TenantKey(key),
        };

        await client.DeleteObjectAsync(request, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetSizeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            using AmazonS3Client client = BuildClient();
            GetObjectMetadataRequest request = new GetObjectMetadataRequest
            {
                BucketName = _opts.BucketName,
                Key = TenantKey(key),
            };

            GetObjectMetadataResponse response = await client.GetObjectMetadataAsync(request, ct)
                .ConfigureAwait(false);
            return response.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return -1L;
        }
    }

    private AmazonS3Client BuildClient()
    {
        RegionEndpoint region = RegionEndpoint.GetBySystemName(_opts.Region);

        if (!string.IsNullOrWhiteSpace(_opts.AccessKeyId) && !string.IsNullOrWhiteSpace(_opts.SecretAccessKey))
        {
            return new AmazonS3Client(_opts.AccessKeyId, _opts.SecretAccessKey, region);
        }

        return new AmazonS3Client(region);
    }

    private string TenantKey(string key)
    {
        Guid tenantId = tenantAccessor.Current?.TenantId
            ?? throw new InvalidOperationException("No tenant context established for S3 blob operation.");
        return $"{tenantId}/{key}";
    }
}
