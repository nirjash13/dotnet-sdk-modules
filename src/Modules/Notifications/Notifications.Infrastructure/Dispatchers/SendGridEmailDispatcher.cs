using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using Notifications.Infrastructure.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Notifications.Infrastructure.Dispatchers;

/// <summary>
/// Email dispatcher backed by the SendGrid Web API.
/// Silent-degrading: exceptions are caught and logged; never propagated.
/// </summary>
internal sealed class SendGridEmailDispatcher(
    IOptions<SendGridOptions> options,
    INotificationTemplate templateRenderer,
    ILogger<SendGridEmailDispatcher> logger)
    : INotificationDispatcher
{
    private readonly SendGridOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (!message.Channels.Contains(NotificationChannel.Email))
        {
            return;
        }

        string recipient = message.RecipientEmail
            ?? throw new InvalidOperationException(
                $"SendGrid dispatcher: no RecipientEmail on message for user {message.RecipientUserId}.");

        string body = message.TemplateKey is not null
            ? templateRenderer.Render(message.TemplateKey, message.TemplateModel ?? new { })
            : message.Body;

        bool isHtml = body.TrimStart().StartsWith("<", StringComparison.Ordinal);

        SendGridClient client = new SendGridClient(_opts.ApiKey);
        SendGridMessage mail = new SendGridMessage
        {
            From = new EmailAddress(_opts.FromEmail, _opts.FromName),
            Subject = message.Subject,
        };

        mail.AddTo(new EmailAddress(recipient));

        if (isHtml)
        {
            mail.HtmlContent = body;
        }
        else
        {
            mail.PlainTextContent = body;
        }

        try
        {
            Response response = await client.SendEmailAsync(mail, ct).ConfigureAwait(false);

            if ((int)response.StatusCode >= 400)
            {
                string responseBody = await response.Body.ReadAsStringAsync(ct).ConfigureAwait(false);
                logger.LogError(
                    "Notifications.SendGrid: failed to send email to {Recipient}. Status={Status}, Body={Body}",
                    recipient, (int)response.StatusCode, responseBody);
            }
            else
            {
                logger.LogInformation(
                    "Notifications.SendGrid: email sent to {Recipient} (type={NotificationType})",
                    recipient, message.NotificationType);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Notifications.SendGrid: exception sending email to {Recipient}", recipient);
        }
    }
}
