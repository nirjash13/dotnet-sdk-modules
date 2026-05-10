using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Paddle billing provider stub (Merchant of Record alternative).
/// TODO(Phase 4): Install Paddle.NET (or use Paddle REST API via HttpClient) and implement.
/// </summary>
public sealed class PaddleBillingProvider : IBillingProvider
{
    /// <inheritdoc />
    public string ProviderName => "paddle";

    /// <inheritdoc />
    public Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Paddle integration — implement CreateCheckoutSessionAsync.");

    /// <inheritdoc />
    public Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId, string returnUrl, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Paddle integration — implement CreatePortalSessionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Paddle integration — implement CancelSubscriptionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Paddle integration — implement PauseSubscriptionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Paddle integration — implement ResumeSubscriptionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId, string newPriceId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Paddle integration — implement UpgradeSubscriptionAsync.");
}
