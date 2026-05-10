using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Contracts;

/// <summary>
/// Integration event published when a tenant's subscription status changes.
/// Consumed by the Entitlements module to invalidate the entitlement cache.
/// </summary>
public sealed class SubscriptionUpdatedIntegrationEvent : IIntegrationEvent
{
    /// <summary>Initializes a new instance of <see cref="SubscriptionUpdatedIntegrationEvent"/>.</summary>
    public SubscriptionUpdatedIntegrationEvent()
    {
    }

    /// <summary>Initializes a new instance with required fields.</summary>
    public SubscriptionUpdatedIntegrationEvent(
        Guid tenantId,
        Guid subscriptionId,
        Guid planId,
        string newStatus,
        DateTimeOffset occurredAt)
    {
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        PlanId = planId;
        NewStatus = newStatus;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the tenant whose subscription changed.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets the subscription identifier.</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>Gets the plan identifier.</summary>
    public Guid PlanId { get; set; }

    /// <summary>Gets the new subscription status (e.g. "Active", "Canceled").</summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the status changed.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}
