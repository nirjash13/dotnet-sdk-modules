using System;
using System.IO;

namespace Files.Infrastructure.Options;

/// <summary>Configuration options for the Files module read from <c>Files</c> config section.</summary>
public sealed class FilesOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Files";

    /// <summary>Gets or sets the root path for the local filesystem blob store.</summary>
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "saasbuilder-blobs");

    /// <summary>Gets or sets the URL prefix for local blob access routes.</summary>
    public string LocalBlobRoutePrefix { get; set; } = "/__local-blob";

    /// <summary>Gets or sets the default hard quota per tenant in bytes (0 = unlimited).</summary>
    public long DefaultHardQuotaBytes { get; set; } = 0;

    /// <summary>Gets or sets the soft warning threshold as a fraction of the hard quota (0.0–1.0).</summary>
    public double SoftQuotaFraction { get; set; } = 0.80;

    // M-O11: presigned upload security controls.

    /// <summary>
    /// Gets or sets the MIME content types allowed in presigned upload requests.
    /// Requests with types outside this list are rejected with 400.
    /// Default: image/png, image/jpeg, application/pdf.
    /// </summary>
    public string[] AllowedUploadContentTypes { get; set; } =
        ["image/png", "image/jpeg", "application/pdf"];

    /// <summary>
    /// Gets or sets the maximum file size in bytes enforced via presigned policy conditions.
    /// Default: 25 MB. 0 = unlimited (not recommended).
    /// </summary>
    public long MaxUploadSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum TTL in minutes for presigned upload URLs.
    /// Caller-supplied values above this ceiling are clamped. Hard maximum: 15 minutes.
    /// </summary>
    public int PresignedUploadMaxTtlMinutes { get; set; } = 15;
}
