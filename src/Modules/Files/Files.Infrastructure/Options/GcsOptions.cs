namespace Files.Infrastructure.Options;

/// <summary>Configuration options for the Google Cloud Storage blob store.</summary>
public sealed class GcsOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Files:Gcs";

    /// <summary>Gets or sets the GCS bucket name.</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>Gets or sets the path to the service account JSON key file (optional — uses ADC when absent).</summary>
    public string? CredentialFile { get; set; }
}
