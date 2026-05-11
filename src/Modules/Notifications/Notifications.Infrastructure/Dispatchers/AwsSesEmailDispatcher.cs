using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using Notifications.Infrastructure.Options;

namespace Notifications.Infrastructure.Dispatchers;

/// <summary>
/// Email dispatcher backed by AWS Simple Email Service v2.
/// Silent-degrading: exceptions are caught and logged; never propagated.
/// </summary>
internal sealed class AwsSesEmailDispatcher(
    IOptions<AwsSesOptions> options,
    INotificationTemplate templateRenderer,
    ILogger<AwsSesEmailDispatcher> logger)
    : INotificationDispatcher
{
    private readonly AwsSesOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (!message.Channels.Contains(NotificationChannel.Email))
        {
            return;
        }

        string recipient = message.RecipientEmail
            ?? throw new InvalidOperationException(
                $"AWS SES dispatcher: no RecipientEmail on message for user {message.RecipientUserId}.");

        string body = message.TemplateKey is not null
            ? templateRenderer.Render(message.TemplateKey, message.TemplateModel ?? new { })
            : message.Body;

        bool isHtml = body.TrimStart().StartsWith("<", StringComparison.Ordinal);

        try
        {
            using AmazonSimpleEmailServiceV2Client client = BuildClient();

            SendEmailRequest request = new SendEmailRequest
            {
                FromEmailAddress = $"{_opts.FromName} <{_opts.FromEmail}>",
                Destination = new Destination
                {
                    ToAddresses = new System.Collections.Generic.List<string> { recipient },
                },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = message.Subject },
                        Body = isHtml
                            ? new Body { Html = new Content { Data = body } }
                            : new Body { Text = new Content { Data = body } },
                    },
                },
            };

            SendEmailResponse response = await client.SendEmailAsync(request, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Notifications.AwsSes: email sent to {Recipient} (type={NotificationType}, messageId={MessageId})",
                recipient, message.NotificationType, response.MessageId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Notifications.AwsSes: exception sending email to {Recipient}", recipient);
        }
    }

    private AmazonSimpleEmailServiceV2Client BuildClient()
    {
        RegionEndpoint region = RegionEndpoint.GetBySystemName(_opts.Region);

        if (!string.IsNullOrWhiteSpace(_opts.AccessKeyId) && !string.IsNullOrWhiteSpace(_opts.SecretAccessKey))
        {
            return new AmazonSimpleEmailServiceV2Client(
                _opts.AccessKeyId,
                _opts.SecretAccessKey,
                region);
        }

        // Fall back to instance profile / environment credentials.
        return new AmazonSimpleEmailServiceV2Client(region);
    }
}
