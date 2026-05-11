using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Chargebee billing provider stub (enterprise billing platform).
///
/// Configure via:
/// - Billing:Chargebee:ApiKey
/// - Billing:Chargebee:Site
/// - Billing:Chargebee:WebhookPassword
/// </summary>
public sealed class ChargebeeBillingProvider : IBillingProvider
{
    /// <inheritdoc />
    public string ProviderName => "chargebee";

    /// <inheritdoc />
    public Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct)
        => Task.FromResult(Result<string>.Failure(
            "Chargebee CreateCheckoutSession is not yet implemented. Use Chargebee Hosted Pages API."));

    /// <inheritdoc />
    public Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId, string returnUrl, CancellationToken ct)
        => Task.FromResult(Result<string>.Failure(
            "Chargebee CreatePortalSession is not yet implemented. Use Chargebee Portal Session API."));

    /// <inheritdoc />
    public Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Chargebee CancelSubscription is not yet implemented."));

    /// <inheritdoc />
    public Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Chargebee PauseSubscription is not yet implemented."));

    /// <inheritdoc />
    public Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Chargebee ResumeSubscription is not yet implemented."));

    /// <inheritdoc />
    public Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId, string newPriceId, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Chargebee UpgradeSubscription is not yet implemented."));

    /// <inheritdoc />
    public Task<Result<ProviderSubscriptionInfo?>> GetSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => Task.FromResult(Result<ProviderSubscriptionInfo?>.Failure(
            "Chargebee GetSubscription is not yet implemented."));

    /// <inheritdoc />
    public Task<Result<bool>> RecordUsageAsync(
        string providerSubscriptionId, string meterId, long quantity, string idempotencyKey, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Chargebee RecordUsage is not yet implemented."));

    /// <inheritdoc />
    public Task<Result<bool>> UpdateQuantityAsync(
        string providerSubscriptionId, int quantity, CancellationToken ct)
        => Task.FromResult(Result<bool>.Failure(
            "Chargebee UpdateQuantity is not yet implemented."));
}
