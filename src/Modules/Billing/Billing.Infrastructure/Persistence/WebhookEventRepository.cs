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

    /// <inheritdoc />
    /// <remarks>
    /// Executes <c>INSERT … ON CONFLICT DO NOTHING</c> and returns whether a row was inserted.
    /// The unique constraint on <c>idempotency_key</c> makes this atomic: exactly one caller
    /// across any number of concurrent replicas will get <c>true</c>.
    /// </remarks>
    public async Task<bool> TryRecordAsync(string idempotencyKey, string eventType, CancellationToken ct)
    {
        // Use parameterized raw SQL for ON CONFLICT DO NOTHING — EF Core does not model
        // upsert semantics via its change-tracker, and we must not use string concatenation.
        int rowsAffected = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO billing.webhook_events (idempotency_key, event_type, provider_name, processed_at)
            VALUES ({idempotencyKey}, {eventType}, 'unknown', NOW() AT TIME ZONE 'UTC')
            ON CONFLICT (idempotency_key) DO NOTHING
            """,
            ct).ConfigureAwait(false);

        return rowsAffected > 0;
    }
}
