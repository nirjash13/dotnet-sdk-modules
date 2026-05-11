using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Application.Models;
using Admin.Infrastructure.Persistence;
using Admin.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Admin.Infrastructure.Services;

/// <summary>
/// EF Core implementation of <see cref="IPendingAdminActionStore"/>.
/// </summary>
public sealed class PendingAdminActionStore(AdminDbContext dbContext) : IPendingAdminActionStore
{
    /// <inheritdoc />
    public async Task<Guid> CreateAsync(PendingAdminAction action, CancellationToken ct = default)
    {
        PendingAdminActionEntity entity = PendingAdminActionEntity.Create(action);
        dbContext.PendingAdminActions.Add(entity);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<PendingAdminAction?> FindAsync(Guid id, CancellationToken ct = default)
    {
        PendingAdminActionEntity? entity = await dbContext.PendingAdminActions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.Status == PendingActionStatus.Pending, ct)
            .ConfigureAwait(false);

        return entity?.ToDomain();
    }

    /// <inheritdoc />
    public async Task ApproveAsync(Guid id, string approverId, CancellationToken ct = default)
    {
        PendingAdminActionEntity? entity = await dbContext.PendingAdminActions
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            entity.Status = PendingActionStatus.Approved;
            entity.ApproverId = approverId;
            entity.ResolvedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DenyAsync(Guid id, string approverId, string reason, CancellationToken ct = default)
    {
        PendingAdminActionEntity? entity = await dbContext.PendingAdminActions
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            .ConfigureAwait(false);

        if (entity is not null)
        {
            entity.Status = PendingActionStatus.Denied;
            entity.ApproverId = approverId;
            entity.DenialReason = reason;
            entity.ResolvedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
