namespace Notifications.Contracts;

/// <summary>Delivery channel for a notification.</summary>
public enum NotificationChannel
{
    /// <summary>Email delivery.</summary>
    Email = 0,

    /// <summary>In-app notification feed.</summary>
    InApp = 1,

    /// <summary>Mobile push notification (APNs / FCM).</summary>
    Push = 2,

    /// <summary>SMS delivery.</summary>
    Sms = 3,

    /// <summary>Outbound webhook delivery.</summary>
    Webhook = 4,
}
