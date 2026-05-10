using System;
using Billing.Domain.DomainEvents;
using Billing.Domain.Exceptions;
using Billing.Domain.ValueObjects;
using SaasBuilder.SharedKernel.Tenancy;

namespace Billing.Domain.Entities;

/// <summary>
/// Aggregate root that tracks a tenant's subscription to a <see cref="Plan"/>.
/// All state mutations go through factory/domain methods to enforce invariants.
/// </summary>
public sealed class Subscription : ITenantScoped
{
    private Subscription()
    {
    }

    /// <summary>Gets the subscription identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Gets the plan this subscription is for.</summary>
    public Guid PlanId { get; private set; }

    /// <summary>Gets the provider-side subscription identifier (e.g., Stripe sub_xxx).</summary>
    public string? ProviderSubscriptionId { get; private set; }

    /// <summary>Gets the current subscription status.</summary>
    public SubscriptionStatus Status { get; private set; }

    /// <summary>Gets the UTC start date of the subscription.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Gets the UTC timestamp when the subscription was canceled (null if not canceled).</summary>
    public DateTimeOffset? CanceledAt { get; private set; }

    /// <summary>Gets the UTC timestamp when the subscription was paused (null if not paused).</summary>
    public DateTimeOffset? PausedAt { get; private set; }

    /// <summary>Gets the UTC end of the trial period (null if not in trial).</summary>
    public DateTimeOffset? TrialEndsAt { get; private set; }

    /// <summary>
    /// Creates a new subscription for a tenant in the <see cref="SubscriptionStatus.Incomplete"/> state.
    /// The status transitions to <see cref="SubscriptionStatus.Active"/> once the provider confirms payment.
    /// </summary>
    public static Subscription Create(
        Guid tenantId,
        Guid planId,
        string? providerSubscriptionId = null,
        DateTimeOffset? trialEndsAt = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new BillingDomainException("Subscription tenantId must not be empty.");
        }

        if (planId == Guid.Empty)
        {
            throw new BillingDomainException("Subscription planId must not be empty.");
        }

        SubscriptionStatus initialStatus = trialEndsAt.HasValue
            ? SubscriptionStatus.Trialing
            : SubscriptionStatus.Incomplete;

        return new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            ProviderSubscriptionId = providerSubscriptionId,
            Status = initialStatus,
            StartedAt = DateTimeOffset.UtcNow,
            TrialEndsAt = trialEndsAt,
        };
    }

    /// <summary>
    /// Activates the subscription after provider payment confirmation.
    /// Returns a domain event for downstream consumption.
    /// </summary>
    public SubscriptionStatusChangedDomainEvent Activate()
    {
        if (Status is SubscriptionStatus.Canceled)
        {
            throw new BillingDomainException("Cannot activate a canceled subscription.");
        }

        Status = SubscriptionStatus.Active;
        return new SubscriptionStatusChangedDomainEvent(Id, TenantId, PlanId, Status);
    }

    /// <summary>Cancels the subscription immediately.</summary>
    public SubscriptionStatusChangedDomainEvent Cancel(DateTimeOffset canceledAt)
    {
        if (Status is SubscriptionStatus.Canceled)
        {
            throw new BillingDomainException("Subscription is already canceled.");
        }

        Status = SubscriptionStatus.Canceled;
        CanceledAt = canceledAt;
        return new SubscriptionStatusChangedDomainEvent(Id, TenantId, PlanId, Status);
    }

    /// <summary>Pauses the subscription.</summary>
    public SubscriptionStatusChangedDomainEvent Pause(DateTimeOffset pausedAt)
    {
        if (Status is not SubscriptionStatus.Active)
        {
            throw new BillingDomainException("Only active subscriptions can be paused.");
        }

        Status = SubscriptionStatus.Paused;
        PausedAt = pausedAt;
        return new SubscriptionStatusChangedDomainEvent(Id, TenantId, PlanId, Status);
    }

    /// <summary>Resumes a paused subscription.</summary>
    public SubscriptionStatusChangedDomainEvent Resume()
    {
        if (Status is not SubscriptionStatus.Paused)
        {
            throw new BillingDomainException("Only paused subscriptions can be resumed.");
        }

        Status = SubscriptionStatus.Active;
        PausedAt = null;
        return new SubscriptionStatusChangedDomainEvent(Id, TenantId, PlanId, Status);
    }

    /// <summary>Upgrades the plan identifier (proration handled by the provider).</summary>
    public SubscriptionStatusChangedDomainEvent UpgradePlan(Guid newPlanId)
    {
        if (newPlanId == Guid.Empty)
        {
            throw new BillingDomainException("New planId must not be empty.");
        }

        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing))
        {
            throw new BillingDomainException("Plan can only be changed on active or trialing subscriptions.");
        }

        PlanId = newPlanId;
        return new SubscriptionStatusChangedDomainEvent(Id, TenantId, PlanId, Status);
    }

    /// <summary>Marks subscription as past-due after a payment failure.</summary>
    public SubscriptionStatusChangedDomainEvent MarkPastDue()
    {
        Status = SubscriptionStatus.PastDue;
        return new SubscriptionStatusChangedDomainEvent(Id, TenantId, PlanId, Status);
    }
}
