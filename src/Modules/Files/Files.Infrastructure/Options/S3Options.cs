namespace Files.Infrastructure.Options;

/// <summary>Configuration options for the AWS S3 blob store.</summary>
public sealed class S3Options
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Files:S3";

    /// <summary>Gets or sets the S3 bucket name.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>Gets or sets the AWS region (e.g. "us-east-1").</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>Gets or sets the AWS access key ID (optional — falls back to instance credentials).</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the AWS secret access key (optional — falls back to instance credentials).</summary>
    public string SecretAccessKey { get; set; } = string.Empty;
}
