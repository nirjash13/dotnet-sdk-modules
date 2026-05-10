using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Notifications.Domain.Entities;

namespace Notifications.Application.Abstractions;

/// <summary>
/// Persistent store for in-app notifications (inbox feed).
/// </summary>
public interface IInAppNotificationStore
{
    /// <summary>Saves a new in-app notification.</summary>
    Task AddAsync(InAppNotification notification, CancellationToken ct = default);

    /// <summary>Returns the inbox for a user, newest first, up to <paramref name="pageSize"/>.</summary>
    Task<IReadOnlyList<InAppNotification>> GetInboxAsync(
        Guid tenantId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Returns a notification by identifier, or <see langword="null"/> if not found.</summary>
    Task<InAppNotification?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persists changes to an existing notification (e.g. marking read).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
