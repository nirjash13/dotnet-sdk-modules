using System;
using Billing.Domain.ValueObjects;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Domain.DomainEvents;

/// <summary>
/// Raised when a subscription's status changes (activated, canceled, paused, etc.).
/// </summary>
public sealed class SubscriptionStatusChangedDomainEvent : IDomainEvent
{
    /// <summary>Initializes the event with all required fields.</summary>
    public SubscriptionStatusChangedDomainEvent(
        Guid subscriptionId,
        Guid tenantId,
        Guid planId,
        SubscriptionStatus newStatus)
    {
        SubscriptionId = subscriptionId;
        TenantId = tenantId;
        PlanId = planId;
        NewStatus = newStatus;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the subscription identifier.</summary>
    public Guid SubscriptionId { get; }

    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the plan identifier at the time of the event.</summary>
    public Guid PlanId { get; }

    /// <summary>Gets the new subscription status.</summary>
    public SubscriptionStatus NewStatus { get; }

    /// <summary>Gets the UTC timestamp when this event occurred.</summary>
    public DateTimeOffset OccurredAt { get; }
}
