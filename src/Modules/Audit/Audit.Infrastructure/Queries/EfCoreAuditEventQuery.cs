using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Contracts;
using Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Queries;

/// <summary>EF Core implementation of <see cref="IAuditEventQuery"/>.</summary>
internal sealed class EfCoreAuditEventQuery(AuditDbContext db) : IAuditEventQuery
{
    /// <inheritdoc />
    public async Task<(int Total, IReadOnlyList<AuditEventDto> Items)> QueryAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? actor,
        string? resource,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Global query filter on SaasBuilderDbContext scopes to tenantId automatically.
        IQueryable<Audit.Infrastructure.Entities.AuditEntry> query = db.AuditEntries
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp);

        if (from.HasValue)
        {
            query = query.Where(e => e.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.Timestamp <= to.Value);
        }

        if (!string.IsNullOrEmpty(actor))
        {
            query = query.Where(e => e.ActorId == actor);
        }

        if (!string.IsNullOrEmpty(resource))
        {
            query = query.Where(e => e.ResourceType == resource);
        }

        int total = await query.CountAsync(ct).ConfigureAwait(false);

        List<AuditEventDto> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AuditEventDto(
                e.Id,
                e.TenantId,
                e.ActorId,
                e.Action,
                e.ResourceType,
                e.ResourceId,
                e.IpAddress,
                e.CorrelationId,
                e.Timestamp))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (total, items);
    }
}
