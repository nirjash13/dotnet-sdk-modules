using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Stripe billing provider stub.
/// TODO(Phase 4): Install Stripe.NET NuGet package and wire <c>IStripeClient</c>.
/// Stub deliberately throws <see cref="NotImplementedException"/> on every call so that
/// integration tests that inject a mock provider do not accidentally invoke real Stripe APIs.
/// </summary>
public sealed class StripeBillingProvider : IBillingProvider
{
    /// <inheritdoc />
    public string ProviderName => "stripe";

    /// <inheritdoc />
    public Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId, string priceId, string successUrl, string cancelUrl, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO(Phase 4): Stripe.NET integration — install Stripe.NET nuget, inject IStripeClient, implement CreateCheckoutSessionAsync.");

    /// <inheritdoc />
    public Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId, string returnUrl, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO(Phase 4): Stripe.NET integration — implement CreatePortalSessionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO(Phase 4): Stripe.NET integration — implement CancelSubscriptionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO(Phase 4): Stripe.NET integration — implement PauseSubscriptionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO(Phase 4): Stripe.NET integration — implement ResumeSubscriptionAsync.");

    /// <inheritdoc />
    public Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId, string newPriceId, CancellationToken ct)
        => throw new NotImplementedException(
            "TODO(Phase 4): Stripe.NET integration — implement UpgradeSubscriptionAsync.");
}
