namespace Notifications.Infrastructure.Options;

/// <summary>Configuration options for the SendGrid email dispatcher.</summary>
public sealed class SendGridOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Notifications:SendGrid";

    /// <summary>Gets or sets the SendGrid API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender email address.</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender display name.</summary>
    public string FromName { get; set; } = "Notifications";
}
