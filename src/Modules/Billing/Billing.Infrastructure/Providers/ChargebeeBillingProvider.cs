using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Chargebee billing provider stub (enterprise billing platform).
/// TODO(Phase 4): Implement using the Chargebee .NET SDK or REST API.
/// </summary>
public sealed class ChargebeeBillingProvider : IBillingProvider
{
    /// <inheritdoc />
    public string ProviderName => "chargebee";

    /// <inheritdoc />
    public Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Chargebee integration.");

    /// <inheritdoc />
    public Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId, string returnUrl, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Chargebee integration.");

    /// <inheritdoc />
    public Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Chargebee integration.");

    /// <inheritdoc />
    public Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Chargebee integration.");

    /// <inheritdoc />
    public Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Chargebee integration.");

    /// <inheritdoc />
    public Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId, string newPriceId, CancellationToken ct)
        => throw new NotImplementedException("TODO(Phase 4): Chargebee integration.");
}
