using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Domain.Entities;

namespace Billing.Application.Abstractions;

/// <summary>
/// Repository contract for <see cref="Subscription"/> aggregate persistence.
/// Implemented in <c>Billing.Infrastructure/Persistence/</c>.
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>Gets the active subscription for a tenant, or null if none exists.</summary>
    Task<Subscription?> FindByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Gets a subscription by its internal identifier.</summary>
    Task<Subscription?> FindByIdAsync(Guid subscriptionId, CancellationToken ct = default);

    /// <summary>Gets a subscription by its provider-side identifier.</summary>
    Task<Subscription?> FindByProviderIdAsync(string providerSubscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Returns all subscriptions in a terminal-failure state whose <c>PaymentFailedAt</c>
    /// is on or before <paramref name="cutoff"/> and whose status is still PastDue
    /// (i.e., not yet suspended or paid). Used by the dunning grace-period scanner.
    /// </summary>
    Task<System.Collections.Generic.IReadOnlyList<Subscription>> FindTerminalFailedBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken ct = default);

    /// <summary>Persists a new subscription.</summary>
    Task AddAsync(Subscription subscription, CancellationToken ct = default);

    /// <summary>Marks a subscription as modified; EF Core tracks changes automatically.</summary>
    void Update(Subscription subscription);

    /// <summary>Saves all tracked changes to the database.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
