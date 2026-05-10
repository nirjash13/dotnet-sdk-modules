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
}
