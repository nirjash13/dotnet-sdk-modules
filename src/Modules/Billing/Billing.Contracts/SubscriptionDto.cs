using System;

namespace Billing.Contracts;

/// <summary>
/// Data transfer object representing a tenant's active subscription.
/// Safe to expose across API boundaries — no domain entity leakage.
/// </summary>
public sealed record SubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid PlanId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CanceledAt,
    DateTimeOffset? PausedAt,
    DateTimeOffset? TrialEndsAt);
