using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using Notifications.Infrastructure.Options;

namespace Notifications.Infrastructure.Dispatchers;

/// <summary>
/// SMTP dispatcher using <see cref="SmtpClient"/> from <c>System.Net.Mail</c>.
/// No external NuGet packages required — System.Net.Mail is part of the BCL.
/// </summary>
internal sealed class SmtpEmailNotificationDispatcher(
    IOptions<SmtpOptions> options,
    INotificationTemplate templateRenderer,
    ILogger<SmtpEmailNotificationDispatcher> logger)
    : INotificationDispatcher
{
    private readonly SmtpOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        // Only handle email channel; skip others silently (other dispatchers may be chained).
        if (!message.Channels.Contains(NotificationChannel.Email))
        {
            return;
        }

        string recipient = message.RecipientEmail
            ?? throw new InvalidOperationException(
                $"NotificationMessage for user {message.RecipientUserId} has no RecipientEmail " +
                "and no user-profile resolver is configured. Set RecipientEmail explicitly.");

        string body = message.TemplateKey is not null
            ? templateRenderer.Render(message.TemplateKey, message.TemplateModel ?? new { })
            : message.Body;

        using MailMessage mail = new MailMessage(
            from: _opts.FromAddress,
            to: recipient,
            subject: message.Subject,
            body: body)
        {
            IsBodyHtml = body.TrimStart().StartsWith("<", StringComparison.Ordinal),
        };

        using SmtpClient client = BuildClient();

        try
        {
            await client.SendMailAsync(mail, ct).ConfigureAwait(false);
            logger.LogInformation(
                "Notifications.Smtp: email sent to {Recipient} (type={NotificationType})",
                recipient, message.NotificationType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log and swallow — dispatcher contract is silent-degrading.
            logger.LogError(
                ex,
                "Notifications.Smtp: failed to send email to {Recipient}",
                recipient);
        }
    }

    private SmtpClient BuildClient()
    {
        SmtpClient client = new SmtpClient(_opts.Host, _opts.Port)
        {
            EnableSsl = _opts.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrEmpty(_opts.Username))
        {
            client.Credentials = new NetworkCredential(_opts.Username, _opts.Password);
        }

        return client;
    }
}
