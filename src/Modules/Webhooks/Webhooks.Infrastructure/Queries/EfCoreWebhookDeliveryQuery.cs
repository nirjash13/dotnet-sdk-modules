using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Webhooks.Application.Abstractions;
using Webhooks.Contracts;
using Webhooks.Infrastructure.Persistence;

namespace Webhooks.Infrastructure.Queries;

/// <summary>EF Core implementation of <see cref="IWebhookDeliveryQuery"/>.</summary>
internal sealed class EfCoreWebhookDeliveryQuery(WebhooksDbContext db) : IWebhookDeliveryQuery
{
    /// <inheritdoc />
    public async Task<(int Total, IReadOnlyList<WebhookDeliveryDto> Items)> QueryAsync(
        Guid? endpointId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Webhooks.Infrastructure.Entities.WebhookDeliveryAttempt> query =
            db.WebhookDeliveryAttempts.AsNoTracking().OrderByDescending(d => d.AttemptedAt);

        if (endpointId.HasValue)
        {
            query = query.Where(d => d.EndpointId == endpointId.Value);
        }

        int total = await query.CountAsync(ct).ConfigureAwait(false);
        List<WebhookDeliveryDto> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new WebhookDeliveryDto(
                d.Id, d.EndpointId, d.EventId, d.StatusCode, d.LatencyMs,
                d.ResponseBody, d.AttemptedAt, d.Succeeded))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (total, items);
    }
}
