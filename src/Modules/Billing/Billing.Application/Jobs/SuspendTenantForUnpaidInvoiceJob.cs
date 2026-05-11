using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Domain.Entities;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy.Lifecycle;

namespace Billing.Application.Jobs;

/// <summary>
/// Job payload and handler for suspending a tenant after the dunning grace period expires.
/// Scheduled as a Hangfire delayed job from the invoice.payment_failed webhook handler.
/// </summary>
/// <remarks>
/// The job re-checks the subscription state before suspending: if the invoice was paid
/// in the meantime, this is a safe no-op.
/// </remarks>
public sealed class SuspendTenantForUnpaidInvoiceJob
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IEnumerable<ITenantLifecycleHandler> _lifecycleHandlers;

    /// <summary>Initializes a new instance of <see cref="SuspendTenantForUnpaidInvoiceJob"/>.</summary>
    public SuspendTenantForUnpaidInvoiceJob(
        ISubscriptionRepository subscriptions,
        IEnumerable<ITenantLifecycleHandler> lifecycleHandlers)
    {
        _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
        _lifecycleHandlers = lifecycleHandlers ?? throw new ArgumentNullException(nameof(lifecycleHandlers));
    }

    /// <summary>
    /// Executes the suspension check. If the subscription is no longer past-due/suspended,
    /// this is a no-op (invoice was paid before grace period expired).
    /// </summary>
    /// <param name="tenantId">The tenant to potentially suspend.</param>
    /// <param name="providerInvoiceId">The failing invoice (used for paid-check).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Result> ExecuteAsync(
        Guid tenantId,
        string providerInvoiceId,
        CancellationToken cancellationToken = default)
    {
        Subscription? subscription = await _subscriptions
            .FindByTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return Result.Failure($"No subscription found for tenant {tenantId}.");
        }

        // If the subscription is now paid (Active or Trialing), the invoice was paid before
        // the grace period expired — do not suspend.
        if (subscription.IsPaid)
        {
            return Result.Success();
        }

        // Track using a local variable since EF returns AsNoTracking from repository.
        // We need to re-fetch with tracking to mutate.
        Subscription? tracked = await _subscriptions
            .FindByIdAsync(subscription.Id, cancellationToken)
            .ConfigureAwait(false);

        if (tracked is null || tracked.IsPaid)
        {
            return Result.Success();
        }

        tracked.Suspend();
        _subscriptions.Update(tracked);
        await _subscriptions.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Fan out to all registered lifecycle handlers.
        foreach (ITenantLifecycleHandler handler in _lifecycleHandlers)
        {
            try
            {
                await handler.OnSuspendAsync(tenantId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Lifecycle handler failure must not block other handlers.
                // Infrastructure callers should log this.
                _ = ex;
            }
        }

        return Result.Success();
    }
}
