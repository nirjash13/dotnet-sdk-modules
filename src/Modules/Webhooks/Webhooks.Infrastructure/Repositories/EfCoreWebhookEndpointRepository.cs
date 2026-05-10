using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Webhooks.Application.Abstractions;
using Webhooks.Domain.Entities;
using Webhooks.Infrastructure.Persistence;

namespace Webhooks.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IWebhookEndpointRepository"/>.</summary>
internal sealed class EfCoreWebhookEndpointRepository(WebhooksDbContext db)
    : IWebhookEndpointRepository
{
    /// <inheritdoc />
    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        await db.WebhookEndpoints.AddAsync(endpoint, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WebhookEndpoint>> GetActiveByEventTypeAsync(
        Guid tenantId,
        string eventType,
        CancellationToken ct = default)
    {
        return db.WebhookEndpoints
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Status == WebhookEndpointStatus.Active)
            .ToListAsync(ct)
            .ContinueWith<IReadOnlyList<WebhookEndpoint>>(
                t => t.Result.Where(e => e.EventTypes.Contains(eventType) || e.EventTypes.Contains("*")).ToList(),
                ct,
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion,
                System.Threading.Tasks.TaskScheduler.Default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WebhookEndpoint>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return db.WebhookEndpoints
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.Status != WebhookEndpointStatus.Deleted)
            .ToListAsync(ct)
            .ContinueWith<IReadOnlyList<WebhookEndpoint>>(
                t => t.Result,
                ct,
                System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion,
                System.Threading.Tasks.TaskScheduler.Default);
    }

    /// <inheritdoc />
    public Task<WebhookEndpoint?> FindAsync(Guid id, CancellationToken ct = default)
        => db.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == id, ct);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
