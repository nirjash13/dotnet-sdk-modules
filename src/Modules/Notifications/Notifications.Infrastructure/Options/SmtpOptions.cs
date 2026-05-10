namespace Notifications.Infrastructure.Options;

/// <summary>SMTP configuration read from <c>Notifications:Smtp</c> config section.</summary>
public sealed class SmtpOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Notifications:Smtp";

    /// <summary>Gets or sets the SMTP host.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP port (default 587).</summary>
    public int Port { get; set; } = 587;

    /// <summary>Gets or sets a value indicating whether TLS/SSL is enabled.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Gets or sets the SMTP username (leave empty for anonymous relay).</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets the SMTP password.</summary>
    public string? Password { get; set; }

    /// <summary>Gets or sets the from address for outbound emails.</summary>
    public string FromAddress { get; set; } = "noreply@example.com";
}
