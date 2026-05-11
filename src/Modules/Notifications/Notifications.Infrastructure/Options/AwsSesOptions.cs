namespace Notifications.Infrastructure.Options;

/// <summary>Configuration options for the AWS SES email dispatcher.</summary>
public sealed class AwsSesOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Notifications:AwsSes";

    /// <summary>Gets or sets the AWS region (e.g. "us-east-1").</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>Gets or sets the AWS access key ID.</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>Gets or sets the AWS secret access key.</summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the verified sender email address.</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender display name.</summary>
    public string FromName { get; set; } = "Notifications";
}
