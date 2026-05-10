using System;
using Notifications.Contracts;
using SaasBuilder.SharedKernel.Tenancy;

namespace Notifications.Domain.Entities;

/// <summary>
/// Per-user per-channel per-type notification preference.
/// Default-deny when a preference row does not exist is configurable at the module level.
/// </summary>
public sealed class NotificationPreference : ITenantScoped
{
    private NotificationPreference()
    {
        // EF Core parameterless constructor.
        NotificationType = string.Empty;
    }

    /// <summary>Initializes a new preference record.</summary>
    public NotificationPreference(
        Guid id,
        Guid tenantId,
        Guid userId,
        NotificationChannel channel,
        string notificationType,
        bool enabled)
    {
        if (string.IsNullOrWhiteSpace(notificationType))
        {
            throw new ArgumentException("NotificationType must not be empty.", nameof(notificationType));
        }

        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Channel = channel;
        NotificationType = notificationType;
        Enabled = enabled;
    }

    /// <summary>Gets the preference identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Gets the user this preference applies to.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the notification channel.</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>Gets the notification type key (e.g. "invoice.payment_due").</summary>
    public string NotificationType { get; private set; }

    /// <summary>Gets or sets whether this channel+type combination is enabled for the user.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Updates the enabled flag.</summary>
    public void SetEnabled(bool enabled) => Enabled = enabled;
}
