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
    /// Upgrades a subscription to a new price/plan at the provider.
    /// </summary>
    Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId,
        string newPriceId,
        CancellationToken ct = default);
}
