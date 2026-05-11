using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;
using Stripe;
using Stripe.Checkout;

namespace Billing.Infrastructure.Providers;

/// <summary>
/// Stripe billing provider — primary billing adapter.
/// Configured via <c>Billing:Stripe:ApiKey</c> and <c>Billing:Stripe:WebhookSecret</c>.
///
/// Customer mapping: the Stripe customer ID should be stored on the Subscription entity
/// as <c>ProviderCustomerId</c> (Phase 4.x enhancement). For now, CreatePortalSessionAsync
/// requires it to be present in configuration under <c>Billing:Stripe:CustomerIdOverride:{tenantId}</c>.
/// </summary>
public sealed class StripeBillingProvider(
    IConfiguration configuration,
    ILogger<StripeBillingProvider> logger)
    : IBillingProvider
{
    // Lazily initialized on first use — API key is validated at call time, not startup.
    // Null until first call. Intentional: we do not throw at registration time.
    private StripeClient? _stripeClient;

    /// <inheritdoc />
    public string ProviderName => "stripe";

    private StripeClient Client
    {
        get
        {
            if (_stripeClient is not null)
            {
                return _stripeClient;
            }

            string? apiKey = configuration["Billing:Stripe:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Billing:Stripe:ApiKey is not configured. Set it via environment variable or appsettings.");
            }

            _stripeClient = new StripeClient(apiKey);
            return _stripeClient;
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreateCheckoutSessionAsync(
        Guid tenantId,
        string priceId,
        string successUrl,
        string cancelUrl,
        CancellationToken ct)
    {
        try
        {
            SessionCreateOptions options = new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1,
                    },
                ],
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    ["tenant_id"] = tenantId.ToString("D"),
                },
            };

            SessionService service = new SessionService(Client);
            Session session = await service.CreateAsync(options, cancellationToken: ct).ConfigureAwait(false);

            logger.LogInformation(
                "Stripe checkout session created: {SessionId} for tenant {TenantId}.",
                session.Id,
                tenantId);

            return Result<string>.Success(session.Url);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error creating checkout session for tenant {TenantId}.", tenantId);
            return Result<string>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreatePortalSessionAsync(
        Guid tenantId,
        string returnUrl,
        CancellationToken ct)
    {
        try
        {
            // The Stripe customer ID must be resolved from the subscription.
            // TODO(Phase 4.x): Fetch from Subscription.ProviderCustomerId once that field is added.
            string? stripeCustomerId = configuration[$"Billing:Stripe:CustomerIdOverride:{tenantId:D}"];
            if (string.IsNullOrWhiteSpace(stripeCustomerId))
            {
                return Result<string>.Failure(
                    "Stripe customer ID not resolvable for this tenant. " +
                    "Store ProviderCustomerId on the Subscription entity (Phase 4.x).");
            }

            Stripe.BillingPortal.SessionCreateOptions options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = stripeCustomerId,
                ReturnUrl = returnUrl,
            };

            Stripe.BillingPortal.SessionService service = new Stripe.BillingPortal.SessionService(Client);
            Stripe.BillingPortal.Session session = await service
                .CreateAsync(options, cancellationToken: ct)
                .ConfigureAwait(false);

            logger.LogInformation("Stripe portal session created for tenant {TenantId}.", tenantId);
            return Result<string>.Success(session.Url);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error creating portal session for tenant {TenantId}.", tenantId);
            return Result<string>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> CancelSubscriptionAsync(
        string providerSubscriptionId,
        bool atPeriodEnd,
        CancellationToken ct)
    {
        try
        {
            SubscriptionService service = new SubscriptionService(Client);
            if (atPeriodEnd)
            {
                SubscriptionUpdateOptions options = new SubscriptionUpdateOptions
                {
                    CancelAtPeriodEnd = true,
                };

                await service.UpdateAsync(providerSubscriptionId, options, cancellationToken: ct).ConfigureAwait(false);
            }
            else
            {
                await service.CancelAsync(providerSubscriptionId, cancellationToken: ct).ConfigureAwait(false);
            }

            logger.LogInformation(
                "Stripe subscription {SubId} canceled (atPeriodEnd={AtPeriodEnd}).",
                providerSubscriptionId,
                atPeriodEnd);

            return Result<bool>.Success(true);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error canceling subscription {SubId}.", providerSubscriptionId);
            return Result<bool>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> PauseSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken ct)
    {
        try
        {
            SubscriptionUpdateOptions options = new SubscriptionUpdateOptions
            {
                PauseCollection = new SubscriptionPauseCollectionOptions
                {
                    Behavior = "void",
                },
            };

            SubscriptionService service = new SubscriptionService(Client);
            await service.UpdateAsync(providerSubscriptionId, options, cancellationToken: ct).ConfigureAwait(false);

            logger.LogInformation("Stripe subscription {SubId} paused.", providerSubscriptionId);
            return Result<bool>.Success(true);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error pausing subscription {SubId}.", providerSubscriptionId);
            return Result<bool>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ResumeSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken ct)
    {
        try
        {
            // Resume by clearing pause_collection.
            SubscriptionUpdateOptions options = new SubscriptionUpdateOptions
            {
                PauseCollection = new SubscriptionPauseCollectionOptions
                {
                    Behavior = string.Empty,
                },
            };

            SubscriptionService service = new SubscriptionService(Client);
            await service.UpdateAsync(providerSubscriptionId, options, cancellationToken: ct).ConfigureAwait(false);

            logger.LogInformation("Stripe subscription {SubId} resumed.", providerSubscriptionId);
            return Result<bool>.Success(true);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error resuming subscription {SubId}.", providerSubscriptionId);
            return Result<bool>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UpgradeSubscriptionAsync(
        string providerSubscriptionId,
        string newPriceId,
        CancellationToken ct)
    {
        try
        {
            SubscriptionService service = new SubscriptionService(Client);
            Subscription subscription = await service
                .GetAsync(providerSubscriptionId, cancellationToken: ct)
                .ConfigureAwait(false);

            if (subscription.Items?.Data == null || subscription.Items.Data.Count == 0)
            {
                return Result<bool>.Failure("Subscription has no items to upgrade.");
            }

            string existingItemId = subscription.Items.Data[0].Id;

            SubscriptionUpdateOptions options = new SubscriptionUpdateOptions
            {
                ProrationBehavior = "create_prorations",
                Items =
                [
                    new SubscriptionItemOptions
                    {
                        Id = existingItemId,
                        Price = newPriceId,
                    },
                ],
            };

            await service.UpdateAsync(providerSubscriptionId, options, cancellationToken: ct).ConfigureAwait(false);

            logger.LogInformation(
                "Stripe subscription {SubId} upgraded to price {PriceId}.",
                providerSubscriptionId,
                newPriceId);

            return Result<bool>.Success(true);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error upgrading subscription {SubId}.", providerSubscriptionId);
            return Result<bool>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ProviderSubscriptionInfo?>> GetSubscriptionAsync(
        string providerSubscriptionId,
        CancellationToken ct)
    {
        try
        {
            SubscriptionService service = new SubscriptionService(Client);
            Subscription subscription = await service
                .GetAsync(providerSubscriptionId, cancellationToken: ct)
                .ConfigureAwait(false);

            long quantity = subscription.Items?.Data?.Count > 0
                ? subscription.Items.Data[0].Quantity
                : 1L;

            // TrialEnd is the best available period boundary in Stripe v51 on the Subscription object.
            DateTimeOffset? periodEnd = subscription.TrialEnd.HasValue && subscription.TrialEnd != default
                ? (DateTimeOffset?)new DateTimeOffset(subscription.TrialEnd.Value, TimeSpan.Zero)
                : null;

            return Result<ProviderSubscriptionInfo?>.Success(new ProviderSubscriptionInfo
            {
                ProviderSubscriptionId = subscription.Id,
                Status = subscription.Status,
                Quantity = (int)quantity,
                CurrentPeriodEnd = periodEnd,
            });
        }
        catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return Result<ProviderSubscriptionInfo?>.Success(null);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error fetching subscription {SubId}.", providerSubscriptionId);
            return Result<ProviderSubscriptionInfo?>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> RecordUsageAsync(
        string providerSubscriptionId,
        string meterId,
        long quantity,
        string idempotencyKey,
        CancellationToken ct)
    {
        // Stripe v51 metered billing uses the Meters API (Stripe.Billing.MeterEventService).
        // The legacy UsageRecord approach is deprecated.
        // TODO(Phase 4.x): Implement via Stripe.Billing.MeterEventService.CreateAsync.
        logger.LogWarning(
            "Stripe RecordUsageAsync not fully implemented (requires Meters API). " +
            "Subscription={SubId}, Meter={MeterId}, Quantity={Qty}.",
            providerSubscriptionId,
            meterId,
            quantity);

        return await Task.FromResult(Result<bool>.Failure(
            "Stripe metered usage recording requires the Meters API (Phase 4.x).")).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UpdateQuantityAsync(
        string providerSubscriptionId,
        int quantity,
        CancellationToken ct)
    {
        try
        {
            SubscriptionService service = new SubscriptionService(Client);
            Subscription subscription = await service
                .GetAsync(providerSubscriptionId, cancellationToken: ct)
                .ConfigureAwait(false);

            if (subscription.Items?.Data == null || subscription.Items.Data.Count == 0)
            {
                return Result<bool>.Failure("Subscription has no items to update quantity for.");
            }

            string itemId = subscription.Items.Data[0].Id;

            SubscriptionUpdateOptions options = new SubscriptionUpdateOptions
            {
                Items =
                [
                    new SubscriptionItemOptions
                    {
                        Id = itemId,
                        Quantity = quantity,
                    },
                ],
                ProrationBehavior = "create_prorations",
            };

            await service.UpdateAsync(providerSubscriptionId, options, cancellationToken: ct).ConfigureAwait(false);

            logger.LogInformation(
                "Stripe subscription {SubId} quantity updated to {Quantity}.",
                providerSubscriptionId,
                quantity);

            return Result<bool>.Success(true);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe error updating quantity for subscription {SubId}.", providerSubscriptionId);
            return Result<bool>.Failure($"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
        }
    }
}
