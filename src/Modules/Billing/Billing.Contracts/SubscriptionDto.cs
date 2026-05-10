using System;

namespace Billing.Contracts;

/// <summary>
/// Data transfer object representing a tenant's active subscription.
/// Safe to expose across API boundaries — no domain entity leakage.
/// </summary>
public sealed class SubscriptionDto
{
    /// <summary>Initializes all required fields.</summary>
    public SubscriptionDto(
        Guid id,
        Guid tenantId,
        Guid planId,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset? canceledAt,
        DateTimeOffset? pausedAt,
        DateTimeOffset? trialEndsAt)
    {
        Id = id;
        TenantId = tenantId;
        PlanId = planId;
        Status = status ?? throw new ArgumentNullException(nameof(status));
        StartedAt = startedAt;
        CanceledAt = canceledAt;
        PausedAt = pausedAt;
        TrialEndsAt = trialEndsAt;
    }

    /// <summary>Gets the subscription identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the plan identifier.</summary>
    public Guid PlanId { get; }

    /// <summary>Gets the subscription status string.</summary>
    public string Status { get; }

    /// <summary>Gets the subscription start date.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets the cancellation date, if canceled.</summary>
    public DateTimeOffset? CanceledAt { get; }

    /// <summary>Gets the pause date, if paused.</summary>
    public DateTimeOffset? PausedAt { get; }

    /// <summary>Gets the trial end date, if in trial.</summary>
    public DateTimeOffset? TrialEndsAt { get; }
}
