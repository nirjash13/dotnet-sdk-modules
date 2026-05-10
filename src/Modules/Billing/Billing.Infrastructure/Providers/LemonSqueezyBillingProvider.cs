using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Lemon Squeezy billing provider stub.
/// TODO(Phase 4): Implement using Lemon Squeezy REST API via typed HttpClient.
/// </summary>
public sealed class LemonSqueezyBillingProvider : IBillingProvider
{
    /// <inheritdoc />
    public string ProviderName => "lemonsqueezy";

    /// <inheritdoc />
    public Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Lemon Squeezy integration.");

    /// <inheritdoc />
    public Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId, string returnUrl, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Lemon Squeezy integration.");

    /// <inheritdoc />
    public Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Lemon Squeezy integration.");

    /// <inheritdoc />
    public Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Lemon Squeezy integration.");

    /// <inheritdoc />
    public Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Lemon Squeezy integration.");

    /// <inheritdoc />
    public Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId, string newPriceId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Lemon Squeezy integration.");
}
