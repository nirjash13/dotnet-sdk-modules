using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Paddle billing provider stub (Merchant of Record alternative).
///
/// Capability matrix vs Stripe:
/// - Checkout sessions: NOT IMPLEMENTED — Paddle uses Paddle.js overlay, not server-side session URLs.
/// - Customer portal: NOT IMPLEMENTED — Paddle provides a self-serve portal via Paddle.js.
/// - Subscription CRUD: NOT IMPLEMENTED — Paddle v2 REST API; install Paddle .NET SDK when available.
/// - Metered usage: NOT IMPLEMENTED — Paddle does not natively support Stripe-style usage records.
/// - Webhooks: NOT IMPLEMENTED — Paddle sends webhooks signed with RSA-SHA256 (not HMAC-SHA256).
///
/// Configure via:
/// - Billing:Paddle:ApiKey
/// - Billing:Paddle:WebhookPublicKey
/// </summary>
public sealed class PaddleBillingProvider : IBillingProvider
{
    /// <inheritdoc />
    public string ProviderName => "paddle";

    /// <inheritdoc />
    /// <remarks>
    /// Paddle uses a client-side Paddle.js integration for checkout, not server-side session URLs.
    /// Use the Paddle.js SDK with your Paddle price ID directly. This method is not supported.
    /// </remarks>
    public Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct)
        => Task.FromResult(Result<string>.Failure(
            "Paddle does not support server-side checkout session creation. Use Paddle.js on the frontend."));

    /// <inheritdoc />
    /// <remarks>
    /// Paddle provides a self-serve customer portal via Paddle.js. This method is not supported.
    /// </remarks>
    public Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId, string returnUrl, CancellationToken ct)
        => Task.FromResult(Result<string>.Failure(
            "Paddle does not expose a server-side portal session URL. Use Paddle.js on the frontend."));

    /// <inheritdoc />
    public Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Paddle CancelSubscription is not yet implemented. Use Paddle v2 REST API to cancel."));

    /// <inheritdoc />
    public Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Paddle PauseSubscription is not yet implemented. Use Paddle v2 REST API to pause."));

    /// <inheritdoc />
    public Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Paddle ResumeSubscription is not yet implemented. Use Paddle v2 REST API to resume."));

    /// <inheritdoc />
    /// <remarks>Proration in Paddle is handled server-side; requires Paddle v2 subscription update API.</remarks>
    public Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId, string newPriceId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Paddle UpgradeSubscription is not yet implemented. Use Paddle v2 REST API to update."));

    /// <inheritdoc />
    public Task<Result<ProviderSubscriptionInfo?>> GetSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<ProviderSubscriptionInfo?>.Failure(
            "Paddle GetSubscription is not yet implemented. Use Paddle v2 REST API to retrieve."));

    /// <inheritdoc />
    /// <remarks>Paddle does not support Stripe-style usage records natively.</remarks>
    public Task<Result<bool>> RecordUsageAsync(
        string providerSubscriptionId, string meterId, long quantity, string idempotencyKey, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Paddle does not support metered usage records in the same way as Stripe."));

    /// <inheritdoc />
    public Task<Result<bool>> UpdateQuantityAsync(
        string providerSubscriptionId, int quantity, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Paddle UpdateQuantity is not yet implemented. Use Paddle v2 REST API to update."));
}
