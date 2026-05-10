using System;
using SaasBuilder.SharedKernel.Tenancy;

namespace Notifications.Domain.Entities;

/// <summary>
/// Persistent in-app notification delivered to a user's inbox feed.
/// Append-only — ReadAt is the only mutable field after creation.
/// </summary>
public sealed class InAppNotification : ITenantScoped
{
    private InAppNotification()
    {
        // EF Core requires a parameterless constructor.
        Title = string.Empty;
        Body = string.Empty;
    }

    /// <summary>Initializes a new in-app notification.</summary>
    public InAppNotification(
        Guid id,
        Guid tenantId,
        Guid userId,
        string title,
        string body,
        string? actionUrl,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title must not be empty.", nameof(title));
        }

        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Title = title;
        Body = body;
        ActionUrl = actionUrl;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the notification identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Gets the owner user identifier.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the short notification title.</summary>
    public string Title { get; private set; }

    /// <summary>Gets the full notification body.</summary>
    public string Body { get; private set; }

    /// <summary>Gets the optional deep-link action URL.</summary>
    public string? ActionUrl { get; private set; }

    /// <summary>Gets the timestamp when this notification was read; null if unread.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>Gets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Marks the notification as read at the given timestamp.</summary>
    public void MarkRead(DateTimeOffset readAt)
    {
        if (ReadAt.HasValue)
        {
            return; // idempotent
        }

        ReadAt = readAt;
    }
}
