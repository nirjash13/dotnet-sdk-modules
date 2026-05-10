using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IWebhookEventRepository"/> for idempotency dedup.
/// </summary>
internal sealed class WebhookEventRepository(BillingDbContext db) : IWebhookEventRepository
{
    public async Task<bool> ExistsAsync(string idempotencyKey, CancellationToken ct)
        => await db.WebhookEvents
            .AsNoTracking()
            .AnyAsync(e => e.IdempotencyKey == idempotencyKey, ct)
            .ConfigureAwait(false);

    public async Task RecordAsync(string idempotencyKey, string eventType, CancellationToken ct)
    {
        db.WebhookEvents.Add(WebhookEvent.Record(idempotencyKey, eventType, providerName: "unknown"));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
