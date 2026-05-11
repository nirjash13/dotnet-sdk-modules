namespace Search.Infrastructure.Options;

/// <summary>Configuration options for the Meilisearch adapter.</summary>
public sealed class MeilisearchOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Search:Meilisearch";

    /// <summary>Gets or sets the Meilisearch server URL (e.g. "http://localhost:7700").</summary>
    public string Url { get; set; } = "http://localhost:7700";

    /// <summary>Gets or sets the Meilisearch API key (master or search key).</summary>
    public string ApiKey { get; set; } = string.Empty;
}
