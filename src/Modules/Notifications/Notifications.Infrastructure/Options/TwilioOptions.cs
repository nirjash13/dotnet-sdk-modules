namespace Notifications.Infrastructure.Options;

/// <summary>Configuration options for the Twilio SMS dispatcher.</summary>
public sealed class TwilioOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Notifications:Twilio";

    /// <summary>Gets or sets the Twilio Account SID.</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Gets or sets the Twilio Auth Token.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Gets or sets the sender phone number (E.164 format, e.g. "+15005550006").</summary>
    public string FromNumber { get; set; } = string.Empty;
}
