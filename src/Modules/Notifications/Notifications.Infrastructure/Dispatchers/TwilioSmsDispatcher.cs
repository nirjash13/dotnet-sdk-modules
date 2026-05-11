using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notifications.Application.Abstractions;
using Notifications.Contracts;
using Notifications.Infrastructure.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Notifications.Infrastructure.Dispatchers;

/// <summary>
/// SMS dispatcher backed by the Twilio Messaging API.
/// Silent-degrading: exceptions are caught and logged; never propagated.
/// Only processes <see cref="NotificationChannel.Sms"/> channel; other channels are passed through.
/// </summary>
internal sealed class TwilioSmsDispatcher(
    IOptions<TwilioOptions> options,
    ILogger<TwilioSmsDispatcher> logger)
    : INotificationDispatcher
{
    private readonly TwilioOptions _opts = options.Value;

    /// <inheritdoc />
    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (!message.Channels.Contains(NotificationChannel.Sms))
        {
            return;
        }

        string? recipientPhone = message.RecipientPhone;
        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            logger.LogWarning(
                "Notifications.Twilio: SMS requested but no RecipientPhone set (userId={UserId}). Skipping.",
                message.RecipientUserId);
            return;
        }

        try
        {
            TwilioClient.Init(_opts.AccountSid, _opts.AuthToken);

            MessageResource msg = await MessageResource.CreateAsync(
                to: new PhoneNumber(recipientPhone),
                from: new PhoneNumber(_opts.FromNumber),
                body: message.Body).ConfigureAwait(false);

            logger.LogInformation(
                "Notifications.Twilio: SMS sent to {Recipient} (type={NotificationType}, sid={Sid})",
                recipientPhone, message.NotificationType, msg.Sid);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Notifications.Twilio: exception sending SMS to {Recipient}", recipientPhone);
        }
    }
}
