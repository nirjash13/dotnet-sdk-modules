using System.Threading;
using System.Threading.Tasks;
using Notifications.Application.Abstractions;
using Notifications.Contracts;

namespace Notifications.Infrastructure.Dispatchers;

// ---------------------------------------------------------------------------
// Cloud / third-party dispatcher stubs — Phase 5.1
// Each stub throws NotImplementedException. Replace with the real implementation
// once the integration NuGet package is vetted and approved in Directory.Packages.props.
// ---------------------------------------------------------------------------

/// <summary>TODO(Phase 5.1): SendGrid email integration — install SendGrid NuGet package.</summary>
internal sealed class SendGridNotificationDispatcher : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): SendGrid integration");
}

/// <summary>TODO(Phase 5.1): AWS SES email integration — install AWSSDK.SimpleEmail NuGet package.</summary>
internal sealed class AwsSesNotificationDispatcher : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): AWS SES integration");
}

/// <summary>TODO(Phase 5.1): Postmark email integration — install PostmarkDotNet NuGet package.</summary>
internal sealed class PostmarkNotificationDispatcher : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): Postmark integration");
}

/// <summary>TODO(Phase 5.1): Resend email integration — install Resend NuGet package.</summary>
internal sealed class ResendNotificationDispatcher : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): Resend integration");
}

/// <summary>TODO(Phase 5.1): Mailgun email integration — install Mailgun.NET NuGet package.</summary>
internal sealed class MailgunNotificationDispatcher : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): Mailgun integration");
}

/// <summary>TODO(Phase 5.1): Twilio SMS integration — install Twilio NuGet package.</summary>
internal sealed class TwilioSmsAdapter : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): Twilio SMS integration");
}

/// <summary>TODO(Phase 5.1): MessageBird SMS integration — install MessageBird NuGet package.</summary>
internal sealed class MessageBirdSmsAdapter : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): MessageBird SMS integration");
}

/// <summary>TODO(Phase 5.1): APNs push integration — install dotAPNS NuGet package.</summary>
internal sealed class ApnsPushAdapter : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): APNs push integration");
}

/// <summary>TODO(Phase 5.1): FCM push integration — install FirebaseAdmin NuGet package.</summary>
internal sealed class FcmPushAdapter : INotificationDispatcher
{
    /// <inheritdoc />
    public Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.1): FCM push integration");
}
