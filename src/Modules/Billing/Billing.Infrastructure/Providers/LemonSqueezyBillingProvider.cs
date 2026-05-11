using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Lemon Squeezy billing provider stub (Merchant of Record alternative).
///
/// Capability matrix vs Stripe:
/// - Checkout: Available via Lemon Squeezy hosted checkout URL — implement via REST API POST /v1/checkouts.
/// - Customer portal: NOT IMPLEMENTED — Lemon Squeezy does not expose a server-side portal session.
/// - Subscription CRUD: NOT IMPLEMENTED — use Lemon Squeezy REST API.
/// - Metered usage: NOT SUPPORTED — Lemon Squeezy does not have Stripe-equivalent metered billing.
/// - Webhooks: Signed with HMAC-SHA256 over X-Signature header — compatible pattern, not yet wired.
///
/// Configure via:
/// - Billing:LemonSqueezy:ApiKey
/// - Billing:LemonSqueezy:StoreId
/// - Billing:LemonSqueezy:WebhookSecret
/// </summary>
public sealed class LemonSqueezyBillingProvider : IBillingProvider
{
    /// <inheritdoc />
    public string ProviderName => "lemonsqueezy";

    /// <inheritdoc />
    /// <remarks>
    /// Lemon Squeezy checkout is available via POST /v1/checkouts. Implementation pending.
    /// API key: Billing:LemonSqueezy:ApiKey. Store ID: Billing:LemonSqueezy:StoreId.
    /// </remarks>
    public Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct)
        => Task.FromResult(Result<string>.Failure(
            "Lemon Squeezy CreateCheckoutSession is not yet implemented. " +
            "Use POST https://api.lemonsqueezy.com/v1/checkouts with your variant_id."));

    /// <inheritdoc />
    /// <remarks>
    /// Lemon Squeezy does not expose a server-side customer portal session API.
    /// Direct customers to app.lemonsqueezy.com/my-orders for self-service.
    /// </remarks>
    public Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId, string returnUrl, CancellationToken ct)
        => Task.FromResult(Result<string>.Failure(
            "Lemon Squeezy does not expose a server-side portal session API."));

    /// <inheritdoc />
    public Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Lemon Squeezy CancelSubscription is not yet implemented. Use DELETE /v1/subscriptions/{id}."));

    /// <inheritdoc />
    public Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Lemon Squeezy PauseSubscription is not yet implemented. Use PATCH /v1/subscriptions/{id} with status=paused."));

    /// <inheritdoc />
    public Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Lemon Squeezy ResumeSubscription is not yet implemented. Use PATCH /v1/subscriptions/{id} with status=active."));

    /// <inheritdoc />
    public Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId, string newPriceId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Lemon Squeezy plan change is not yet implemented. Use PATCH /v1/subscriptions/{id} with variant_id."));

    /// <inheritdoc />
    public Task<Result<ProviderSubscriptionInfo?>> GetSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<ProviderSubscriptionInfo?>.Failure(
            "Lemon Squeezy GetSubscription is not yet implemented. Use GET /v1/subscriptions/{id}."));

    /// <inheritdoc />
    /// <remarks>Lemon Squeezy does not have Stripe-equivalent usage records.</remarks>
    public Task<Result<bool>> RecordUsageAsync(
        string providerSubscriptionId, string meterId, long quantity, string idempotencyKey, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Lemon Squeezy does not support metered usage records."));

    /// <inheritdoc />
    public Task<Result<bool>> UpdateQuantityAsync(
        string providerSubscriptionId, int quantity, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Lemon Squeezy UpdateQuantity is not yet implemented."));
}
