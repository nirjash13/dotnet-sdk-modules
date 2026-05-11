namespace Notifications.Infrastructure.Options;

/// <summary>Configuration options for the Postmark email dispatcher.</summary>
public sealed class PostmarkOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Notifications:Postmark";

    /// <summary>Gets or sets the Postmark server token.</summary>
    public string ServerToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender email address (must be a verified Postmark sender).</summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender display name.</summary>
    public string FromName { get; set; } = "Notifications";
}
