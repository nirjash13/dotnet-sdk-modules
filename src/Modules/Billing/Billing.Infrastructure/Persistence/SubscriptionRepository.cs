using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Domain.Entities;
using Billing.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ISubscriptionRepository"/>.
/// </summary>
internal sealed class SubscriptionRepository(BillingDbContext db) : ISubscriptionRepository
{
    public async Task<Subscription?> FindByTenantAsync(Guid tenantId, CancellationToken ct)
        => await db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            .ConfigureAwait(false);

    public async Task<Subscription?> FindByIdAsync(Guid subscriptionId, CancellationToken ct)
        => await db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct)
            .ConfigureAwait(false);

    public async Task<Subscription?> FindByProviderIdAsync(string providerSubscriptionId, CancellationToken ct)
        => await db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == providerSubscriptionId, ct)
            .ConfigureAwait(false);

    public async Task AddAsync(Subscription subscription, CancellationToken ct)
        => await db.Subscriptions.AddAsync(subscription, ct).ConfigureAwait(false);

    public void Update(Subscription subscription)
        => db.Subscriptions.Update(subscription);

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<Subscription>> FindTerminalFailedBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken ct)
        => await db.Subscriptions
            .AsNoTracking()
            .Where(s =>
                s.Status == SubscriptionStatus.PastDue
                && s.PaymentFailedAt != null
                && s.TerminalFailedInvoiceId != null
                && s.PaymentFailedAt <= cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
