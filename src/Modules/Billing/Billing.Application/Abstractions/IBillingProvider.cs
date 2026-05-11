using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Abstractions;

/// <summary>
/// Strategy abstraction over external billing providers (Stripe, Paddle, Lemon Squeezy, Chargebee).
/// Implementations live in <c>Billing.Infrastructure/Providers/</c>.
/// </summary>
/// <remarks>
/// Only the methods needed for the Phase 4 scaffold are declared here.
/// Additional capability methods (metered usage, coupons, referrals, etc.) will be added in Phase 4.x.
/// </remarks>
public interface IBillingProvider
{
    /// <summary>
    /// Gets the machine-readable provider name (e.g., "stripe", "paddle").
    /// Used to dispatch webhook events to the correct handler.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Creates a hosted checkout session and returns the redirect URL.
    /// </summary>
    Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId,
        string priceId,
        string successUrl,
        string cancelUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a customer portal session and returns the redirect URL.
    /// </summary>
    Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId,
        string returnUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels a provider-side subscription.
    /// </summary>
    Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId,
        bool atPeriodEnd,
        CancellationToken ct = default);

    /// <summary>
    /// Pauses a provider-side subscription.
    /// </summary>
    Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken ct = default);

    /// <summary>
    /// Resumes a previously paused provider-side subscription.
    /// </summary>
    Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken ct = default);

    /// <summary>
    /// Upgrades a subscription to a new price/plan at the provider with proration.
    /// </summary>
    Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId,
        string newPriceId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current state of a subscription from the provider.
    /// Returns null inside the result when the subscription is not found at the provider.
    /// </summary>
    Task<Result<ProviderSubscriptionInfo?>> GetSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken ct = default);

    /// <summary>
    /// Records metered usage against a billing meter (for usage-based billing).
    /// </summary>
    /// <param name="providerSubscriptionId">The provider subscription identifier.</param>
    /// <param name="meterId">The provider meter/price identifier.</param>
    /// <param name="quantity">The quantity to record.</param>
    /// <param name="idempotencyKey">Unique key to make the call safe to retry.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result<bool>> RecordUsageAsync(
        string providerSubscriptionId,
        string meterId,
        long quantity,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the seat quantity on a subscription (for per-seat billing).
    /// </summary>
    Task<Result<bool>> UpdateQuantityAsync(
        string providerSubscriptionId,
        int quantity,
        CancellationToken ct = default);
}

/// <summary>
/// Lightweight snapshot of a subscription as seen by the billing provider.
/// Used during reconciliation to compare against the local DB state.
/// </summary>
public sealed class ProviderSubscriptionInfo
{
    /// <summary>Gets or sets the provider-side subscription identifier.</summary>
    public required string ProviderSubscriptionId { get; init; }

    /// <summary>Gets or sets the provider-reported status (e.g., "active", "canceled", "past_due").</summary>
    public required string Status { get; init; }

    /// <summary>Gets or sets the current quantity (seats).</summary>
    public int Quantity { get; init; } = 1;

    /// <summary>Gets or sets the current period end date, if available.</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; init; }
}
