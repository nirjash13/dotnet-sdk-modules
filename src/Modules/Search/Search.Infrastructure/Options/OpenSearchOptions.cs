namespace Search.Infrastructure.Options;

/// <summary>Configuration options for the OpenSearch adapter.</summary>
public sealed class OpenSearchOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Search:OpenSearch";

    /// <summary>Gets or sets the OpenSearch node URI (e.g. "http://localhost:9200").</summary>
    public string Uri { get; set; } = "http://localhost:9200";

    /// <summary>Gets or sets the index prefix; all indexes are named <c>{Prefix}_{indexName}</c>.</summary>
    public string IndexPrefix { get; set; } = "saasbuilder";

    /// <summary>Gets or sets the username for basic auth (optional).</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets the password for basic auth (optional).</summary>
    public string? Password { get; set; }
}
