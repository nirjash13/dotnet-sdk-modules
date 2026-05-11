using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using Notifications.Infrastructure.Options;
using PostmarkDotNet;

namespace Notifications.Infrastructure.Dispatchers;

/// <summary>
/// Email dispatcher backed by the Postmark transactional email API.
/// Silent-degrading: exceptions are caught and logged; never propagated.
/// </summary>
internal sealed class PostmarkEmailDispatcher(
    IOptions<PostmarkOptions> options,
    INotificationTemplate templateRenderer,
    ILogger<PostmarkEmailDispatcher> logger)
    : INotificationDispatcher
{
    private readonly PostmarkOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (!message.Channels.Contains(NotificationChannel.Email))
        {
            return;
        }

        string recipient = message.RecipientEmail
            ?? throw new InvalidOperationException(
                $"Postmark dispatcher: no RecipientEmail on message for user {message.RecipientUserId}.");

        string body = message.TemplateKey is not null
            ? templateRenderer.Render(message.TemplateKey, message.TemplateModel ?? new { })
            : message.Body;

        bool isHtml = body.TrimStart().StartsWith("<", StringComparison.Ordinal);

        PostmarkMessage mail = new PostmarkMessage
        {
            From = $"{_opts.FromName} <{_opts.FromEmail}>",
            To = recipient,
            Subject = message.Subject,
            TextBody = isHtml ? null : body,
            HtmlBody = isHtml ? body : null,
        };

        try
        {
            PostmarkClient client = new PostmarkClient(_opts.ServerToken);
            PostmarkResponse response = await client.SendMessageAsync(mail).ConfigureAwait(false);

            if (response.Status != PostmarkStatus.Success)
            {
                logger.LogError(
                    "Notifications.Postmark: failed to send email to {Recipient}. ErrorCode={ErrorCode}, Message={Message}",
                    recipient, response.ErrorCode, response.Message);
            }
            else
            {
                logger.LogInformation(
                    "Notifications.Postmark: email sent to {Recipient} (type={NotificationType}, messageId={MessageId})",
                    recipient, message.NotificationType, response.MessageID);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Notifications.Postmark: exception sending email to {Recipient}", recipient);
        }
    }
}
