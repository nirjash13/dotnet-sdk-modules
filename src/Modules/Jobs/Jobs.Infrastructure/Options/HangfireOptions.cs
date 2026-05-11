namespace Jobs.Infrastructure.Options;

/// <summary>Configuration options for the Hangfire job scheduler.</summary>
public sealed class HangfireOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Jobs:Hangfire";

    /// <summary>Gets or sets the PostgreSQL connection string for Hangfire's storage.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the schema name for Hangfire tables. Default is "hangfire".</summary>
    public string SchemaName { get; set; } = "hangfire";

    /// <summary>Gets or sets the number of Hangfire worker threads. Default is 5.</summary>
    public int WorkerCount { get; set; } = 5;
}
