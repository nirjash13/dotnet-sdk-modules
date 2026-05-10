using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IInAppNotificationStore"/>.
/// All reads use <c>AsNoTracking()</c>; write tracking is done via tracked entities on SaveChanges.
/// </summary>
internal sealed class EfCoreInAppNotificationStore(NotificationsDbContext db)
    : IInAppNotificationStore
{
    /// <inheritdoc />
    public async Task AddAsync(InAppNotification notification, CancellationToken ct = default)
    {
        await db.InAppNotifications.AddAsync(notification, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<InAppNotification>> GetInboxAsync(
        Guid tenantId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Tenant filter applied by global query filter on SaasBuilderDbContext.
        return db.InAppNotifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ContinueWith<IReadOnlyList<InAppNotification>>(
                t => t.Result,
                ct,
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion,
                System.Threading.Tasks.TaskScheduler.Default);
    }

    /// <inheritdoc />
    public Task<InAppNotification?> FindAsync(Guid id, CancellationToken ct = default)
        => db.InAppNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
