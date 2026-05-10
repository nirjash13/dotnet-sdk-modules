using System;

namespace Notifications.Contracts;

/// <summary>DTO returned by the in-app inbox API.</summary>
/// <param name="Id">Notification identifier.</param>
/// <param name="UserId">Owner user identifier.</param>
/// <param name="Title">Short title displayed in the notification feed.</param>
/// <param name="Body">Full body text.</param>
/// <param name="ActionUrl">Optional deep-link URL.</param>
/// <param name="ReadAt">Timestamp when the notification was read; null if unread.</param>
/// <param name="CreatedAt">Timestamp when the notification was created.</param>
public record InAppNotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Body,
    string? ActionUrl,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);
