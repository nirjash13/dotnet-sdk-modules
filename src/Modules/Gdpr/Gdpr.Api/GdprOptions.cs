namespace Gdpr.Api;

/// <summary>Configuration options for the GDPR module.</summary>
public sealed class GdprOptions
{
    /// <summary>Gets or sets the grace period in days before an erasure request is processed. Default is 30.</summary>
    public int ErasureGraceDays { get; set; } = 30;
}
