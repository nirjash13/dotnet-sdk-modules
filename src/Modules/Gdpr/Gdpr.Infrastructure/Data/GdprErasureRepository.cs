using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Application.Abstractions;
using Gdpr.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Gdpr.Infrastructure.Data;

/// <summary>EF Core implementation of <see cref="IGdprErasureRepository"/>.</summary>
internal sealed class GdprErasureRepository : IGdprErasureRepository
{
    private readonly GdprDbContext _db;

    public GdprErasureRepository(GdprDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<ErasureRequestDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset graceEndsAt,
        CancellationToken ct = default)
    {
        var entity = new GdprErasureRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Status = ErasureStatus.Pending,
            RequestedAt = DateTimeOffset.UtcNow,
            GraceEndsAt = graceEndsAt,
        };

        _db.ErasureRequests.Add(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(Guid id, CancellationToken ct = default)
    {
        GdprErasureRequest? entity = await _db.ErasureRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.Status == ErasureStatus.Pending, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        entity.Status = ErasureStatus.Cancelled;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ErasureRequestDto>> GetOverdueAsync(
        DateTimeOffset asOf,
        CancellationToken ct = default)
    {
        return await _db.ErasureRequests
            .AsNoTracking()
            .Where(r => r.Status == ErasureStatus.Pending && r.GraceEndsAt <= asOf)
            .Select(r => new ErasureRequestDto(
                r.Id,
                r.UserId,
                r.TenantId,
                r.Status,
                r.RequestedAt,
                r.GraceEndsAt,
                r.CompletedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task MarkCompletedAsync(Guid id, DateTimeOffset completedAt, CancellationToken ct = default)
    {
        GdprErasureRequest? entity = await _db.ErasureRequests
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.Status = ErasureStatus.Completed;
        entity.CompletedAt = completedAt;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ErasureRequestDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ErasureRequests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ErasureRequestDto(
                r.Id,
                r.UserId,
                r.TenantId,
                r.Status,
                r.RequestedAt,
                r.GraceEndsAt,
                r.CompletedAt))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    private static ErasureRequestDto ToDto(GdprErasureRequest e) =>
        new ErasureRequestDto(e.Id, e.UserId, e.TenantId, e.Status, e.RequestedAt, e.GraceEndsAt, e.CompletedAt);
}
