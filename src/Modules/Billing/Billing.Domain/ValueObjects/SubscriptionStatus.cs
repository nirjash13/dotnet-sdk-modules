namespace Billing.Domain.ValueObjects;

/// <summary>
/// Subscription lifecycle statuses — mirrors the Stripe subscription status states.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>The subscription is in a free trial period.</summary>
    Trialing = 0,

    /// <summary>The subscription is active and paid.</summary>
    Active = 1,

    /// <summary>A payment has failed and the grace period is active.</summary>
    PastDue = 2,

    /// <summary>The subscription has been canceled.</summary>
    Canceled = 3,

    /// <summary>The subscription is created but not yet paid (e.g., awaiting first invoice).</summary>
    Incomplete = 4,

    /// <summary>The subscription is temporarily paused.</summary>
    Paused = 5,
}
