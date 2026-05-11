namespace Files.Infrastructure.Options;

/// <summary>Configuration options for the Azure Blob Storage blob store.</summary>
public sealed class AzureBlobOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Files:AzureBlob";

    /// <summary>Gets or sets the Azure Storage connection string.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Gets or sets the container name prefix. Each tenant gets <c>{Prefix}-{tenantId}</c>.</summary>
    public string ContainerPrefix { get; set; } = "saasbuilder";
}
