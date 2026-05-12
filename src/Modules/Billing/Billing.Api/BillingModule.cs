using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Contracts;
using Billing.Infrastructure.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Billing.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the Billing module.
/// </summary>
public sealed class BillingModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddBillingInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder billing = endpoints
            .MapGroup("/api/v1/billing")
            .WithTags("billing")
            .RequireAuthorization();

        // POST /api/v1/billing/checkout/session
        billing.MapPost("/checkout/session", CreateCheckoutSessionAsync)
            .WithName("Billing_CreateCheckoutSession")
            .WithSummary("Creates a hosted checkout session and returns the provider redirect URL.");

        // POST /api/v1/billing/customer-portal/session
        billing.MapPost("/customer-portal/session", CreatePortalSessionAsync)
            .WithName("Billing_CreatePortalSession")
            .WithSummary("Creates a customer portal session. Requires billing.manage permission.");

        // GET /api/v1/billing/me/subscription
        billing.MapGet("/me/subscription", GetMySubscriptionAsync)
            .WithName("Billing_GetMySubscription")
            .WithSummary("Returns the current tenant's active subscription.");

        // GET /api/v1/billing/me/invoices
        billing.MapGet("/me/invoices", GetMyInvoicesAsync)
            .WithName("Billing_GetMyInvoices")
            .WithSummary("Returns the paginated invoice list for the current tenant.");

        // GET /api/v1/billing/me/usage
        billing.MapGet("/me/usage", GetMyUsageAsync)
            .WithName("Billing_GetMyUsage")
            .WithSummary("Returns current period metered usage for the current tenant.");

        // POST /api/v1/billing/subscription:upgrade
        billing.MapPost("/subscription:upgrade", UpgradeSubscriptionAsync)
            .WithName("Billing_UpgradeSubscription")
            .WithSummary("Upgrades the current subscription to a new plan with proration.");

        // POST /api/v1/billing/subscription:cancel
        billing.MapPost("/subscription:cancel", CancelSubscriptionAsync)
            .WithName("Billing_CancelSubscription")
            .WithSummary("Cancels the current subscription (at period end or immediately).");

        // POST /api/v1/billing/webhook/{provider} — public; signature verified inside
        billing.MapPost("/webhook/{provider}", ReceiveWebhookAsync)
            .WithName("Billing_ReceiveWebhook")
            .WithSummary("Receives inbound provider webhooks. Verifies HMAC signature before processing.")
            .AllowAnonymous();
    }

    private static async Task<IResult> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        IBillingProvider billingProvider,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request?.PriceId))
        {
            return Results.BadRequest(Problem("priceId is required."));
        }

        Result<string> result = await billingProvider.CreateCheckoutSessionAsync(
            tenant.TenantId,
            request.PriceId,
            request.SuccessUrl ?? "/billing/success",
            request.CancelUrl ?? "/billing/cancel",
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Billing provider error");
        }

        return Results.Ok(new { Url = result.Value });
    }

    private static async Task<IResult> CreatePortalSessionAsync(
        PortalSessionRequest request,
        IBillingProvider billingProvider,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        string returnUrl = request?.ReturnUrl ?? "/billing";

        Result<string> result = await billingProvider.CreatePortalSessionAsync(
            tenant.TenantId,
            returnUrl,
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Billing provider error");
        }

        return Results.Ok(new { Url = result.Value });
    }

    private static async Task<IResult> GetMySubscriptionAsync(
        ITenantContextAccessor tenantAccessor,
        ISubscriptionRepository subscriptions,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        Billing.Domain.Entities.Subscription? subscription = await subscriptions
            .FindByTenantAsync(tenant.TenantId, ct)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return Results.NotFound(new { Detail = "No active subscription found for this tenant." });
        }

        return Results.Ok(new SubscriptionDto(
            subscription.Id,
            subscription.TenantId,
            subscription.PlanId,
            subscription.ProviderSubscriptionId,
            subscription.Status.ToString(),
            subscription.StartedAt,
            subscription.TrialEndsAt,
            subscription.CanceledAt));
    }

    private static async Task<IResult> GetMyInvoicesAsync(
        ITenantContextAccessor tenantAccessor,
        IBillingProvider billingProvider,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        // Invoice listing requires a dedicated provider method not yet in IBillingProvider.
        // Return 501 with a documented gap rather than silently returning empty.
        // TODO(Phase 4.x): Add IBillingProvider.ListInvoicesAsync(tenantId, page, pageSize) and implement.
        await Task.CompletedTask.ConfigureAwait(false);
        return Results.Problem(
            detail: "Invoice listing from provider is not yet implemented. Add IBillingProvider.ListInvoicesAsync.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static async Task<IResult> GetMyUsageAsync(
        ITenantContextAccessor tenantAccessor,
        IBillingProvider billingProvider,
        CancellationToken ct = default)
    {
        // Usage retrieval requires a dedicated provider method not yet in IBillingProvider.
        // TODO(Phase 4.x): Add IBillingProvider.GetCurrentPeriodUsageAsync(tenantId) and implement.
        await Task.CompletedTask.ConfigureAwait(false);
        return Results.Problem(
            detail: "Usage retrieval from provider is not yet implemented. Add IBillingProvider.GetCurrentPeriodUsageAsync.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static async Task<IResult> UpgradeSubscriptionAsync(
        UpgradeRequest request,
        ISubscriptionRepository subscriptions,
        IBillingProvider billingProvider,
        ITenantContextAccessor tenantAccessor,
        UpgradePlanHandler handler,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request?.NewPriceId))
        {
            return Results.BadRequest(Problem("newPriceId is required."));
        }

        Billing.Domain.Entities.Subscription? subscription = await subscriptions
            .FindByTenantAsync(tenant.TenantId, ct)
            .ConfigureAwait(false);

        if (subscription is null || string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            return Results.NotFound(new { Detail = "No active subscription with a provider ID found." });
        }

        Result<bool> providerResult = await billingProvider.UpgradeSubscriptionAsync(
            subscription.ProviderSubscriptionId,
            request.NewPriceId,
            ct).ConfigureAwait(false);

        if (!providerResult.IsSuccess)
        {
            return Results.Problem(
                detail: providerResult.Error,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Provider upgrade failed");
        }

        return Results.Ok(new { Message = "Subscription upgraded successfully." });
    }

    private static async Task<IResult> CancelSubscriptionAsync(
        CancelRequest request,
        ISubscriptionRepository subscriptions,
        IBillingProvider billingProvider,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        Billing.Domain.Entities.Subscription? subscription = await subscriptions
            .FindByTenantAsync(tenant.TenantId, ct)
            .ConfigureAwait(false);

        if (subscription is null || string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            return Results.NotFound(new { Detail = "No active subscription found." });
        }

        bool atPeriodEnd = request?.AtPeriodEnd ?? true;

        Result<bool> result = await billingProvider.CancelSubscriptionAsync(
            subscription.ProviderSubscriptionId,
            atPeriodEnd,
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Provider cancel failed");
        }

        return Results.Ok(new { Message = atPeriodEnd ? "Subscription will cancel at period end." : "Subscription canceled immediately." });
    }

    private static async Task<IResult> ReceiveWebhookAsync(
        string provider,
        HttpRequest httpRequest,
        IServiceProvider services,
        IWebhookEventRepository webhookEvents,
        [Microsoft.AspNetCore.Mvc.FromServices] IPublishEndpoint publishEndpoint,
        ILogger<BillingModule> logger,
        CancellationToken ct)
    {
        // 1. Read raw body.
        using MemoryStream ms = new MemoryStream();
        await httpRequest.Body.CopyToAsync(ms, ct).ConfigureAwait(false);
        byte[] rawBody = ms.ToArray();

        // 2. Resolve signature verifier for the provider.
        IWebhookSignatureVerifier? verifier = null;
        foreach (IWebhookSignatureVerifier v in services.GetServices<IWebhookSignatureVerifier>())
        {
            if (string.Equals(v.ProviderName, provider, StringComparison.OrdinalIgnoreCase))
            {
                verifier = v;
                break;
            }
        }

        if (verifier is null)
        {
            return Results.Problem(
                detail: $"No webhook signature verifier registered for provider '{provider}'.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown provider");
        }

        // 3. Verify signature (includes 5-minute timestamp check).
        // StringValues.ToString() returns "" (not null) when the header is absent, so ?? never fires.
        // Use explicit presence checks so non-Stripe providers (Lemon Squeezy, Paddle) can supply X-Signature.
        string signatureHeader =
            httpRequest.Headers.TryGetValue("Stripe-Signature", out Microsoft.Extensions.Primitives.StringValues stripeSig) && stripeSig.Count > 0
                ? stripeSig.ToString()
                : httpRequest.Headers.TryGetValue("X-Signature", out Microsoft.Extensions.Primitives.StringValues xSig) && xSig.Count > 0
                    ? xSig.ToString()
                    : string.Empty;

        WebhookVerificationResult verification = await verifier
            .VerifyAsync(rawBody, signatureHeader, ct)
            .ConfigureAwait(false);

        if (!verification.IsValid)
        {
            logger.LogWarning(
                "Webhook signature verification failed for provider {Provider}: {Reason}",
                provider,
                verification.FailureReason);
            return Results.Problem(
                detail: verification.FailureReason,
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Webhook signature invalid");
        }

        // 4. Idempotency check.
        string idempotencyKey = verification.IdempotencyKey!;
        if (await webhookEvents.ExistsAsync(idempotencyKey, ct).ConfigureAwait(false))
        {
            logger.LogInformation(
                "Duplicate webhook received (idempotencyKey={Key}); returning 200 without reprocessing.",
                idempotencyKey);
            return Results.Ok(new { Message = "Already processed." });
        }

        // 5. Record for idempotency before processing to prevent double-processing on crash.
        await webhookEvents.RecordAsync(idempotencyKey, verification.EventType!, ct).ConfigureAwait(false);

        // 6. Dispatch integration events based on Stripe event type.
        await DispatchStripeEventAsync(
            verification.EventType!,
            rawBody,
            services,
            publishEndpoint,
            logger,
            ct).ConfigureAwait(false);

        logger.LogInformation(
            "Webhook received from {Provider}: eventType={EventType}, key={Key}.",
            provider,
            verification.EventType,
            idempotencyKey);

        return Results.Ok(new { Message = "Accepted." });
    }

    private static async Task DispatchStripeEventAsync(
        string eventType,
        byte[] rawBody,
        IServiceProvider services,
        IPublishEndpoint publishEndpoint,
        ILogger logger,
        CancellationToken ct)
    {
        // Parse the JSON body once — needed to resolve tenantId and invoice fields.
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(rawBody);
        System.Text.Json.JsonElement root = doc.RootElement;

        switch (eventType)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
            {
                // Resolve tenantId from subscription metadata["tenant_id"] written during checkout.
                Guid tenantId = TryGetTenantIdFromSubscriptionObject(root);
                if (tenantId == Guid.Empty)
                {
                    logger.LogWarning(
                        "Stripe {EventType}: could not resolve tenant_id from subscription metadata; skipping state mutation.",
                        eventType);
                    break;
                }

                ISubscriptionRepository subscriptions = services.GetRequiredService<ISubscriptionRepository>();
                Billing.Domain.Entities.Subscription? subscription =
                    await subscriptions.FindByTenantAsync(tenantId, ct).ConfigureAwait(false);

                if (subscription is null)
                {
                    logger.LogWarning(
                        "Stripe {EventType}: no local subscription for tenant {TenantId}.",
                        eventType,
                        tenantId);
                    break;
                }

                if (eventType == "customer.subscription.deleted")
                {
                    // Re-fetch with tracking to mutate.
                    Billing.Domain.Entities.Subscription? tracked =
                        await subscriptions.FindByIdAsync(subscription.Id, ct).ConfigureAwait(false);
                    if (tracked is not null && tracked.Status is not Billing.Domain.ValueObjects.SubscriptionStatus.Canceled)
                    {
                        tracked.Cancel(DateTimeOffset.UtcNow);
                        subscriptions.Update(tracked);
                        await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }

                logger.LogInformation("Stripe {EventType} processed for tenant {TenantId}.", eventType, tenantId);
                break;
            }

            case "invoice.paid":
            {
                // Clear dunning state on successful payment so grace-period job becomes a no-op.
                Guid tenantId = TryGetTenantIdFromInvoiceObject(root);
                if (tenantId == Guid.Empty)
                {
                    logger.LogWarning("Stripe invoice.paid: could not resolve tenant_id; skipping dunning clear.");
                    break;
                }

                ISubscriptionRepository subscriptions = services.GetRequiredService<ISubscriptionRepository>();
                Billing.Domain.Entities.Subscription? subscription =
                    await subscriptions.FindByTenantAsync(tenantId, ct).ConfigureAwait(false);

                if (subscription is null)
                {
                    logger.LogInformation("Stripe invoice.paid: no local subscription for tenant {TenantId}.", tenantId);
                    break;
                }

                Billing.Domain.Entities.Subscription? tracked =
                    await subscriptions.FindByIdAsync(subscription.Id, ct).ConfigureAwait(false);

                if (tracked is not null && !tracked.IsPaid)
                {
                    tracked.MarkPaid();
                    subscriptions.Update(tracked);
                    await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);
                    logger.LogInformation(
                        "Dunning cleared for tenant {TenantId} after invoice.paid.",
                        tenantId);
                }

                break;
            }

            case "invoice.payment_failed":
            {
                Guid tenantId = TryGetTenantIdFromInvoiceObject(root);
                System.Text.Json.JsonElement invoiceObj = GetStripeDataObject(root);

                string providerInvoiceId = invoiceObj.TryGetProperty("id", out System.Text.Json.JsonElement invoiceIdEl)
                    ? invoiceIdEl.GetString() ?? "unknown"
                    : "unknown";

                long amountDue = invoiceObj.TryGetProperty("amount_due", out System.Text.Json.JsonElement amtEl)
                    ? amtEl.GetInt64()
                    : 0;

                string currency = invoiceObj.TryGetProperty("currency", out System.Text.Json.JsonElement currEl)
                    ? currEl.GetString() ?? "usd"
                    : "usd";

                bool isTerminal = invoiceObj.TryGetProperty("next_payment_attempt", out System.Text.Json.JsonElement npaEl)
                    && npaEl.ValueKind == System.Text.Json.JsonValueKind.Null;

                int attemptCount = invoiceObj.TryGetProperty("attempt_count", out System.Text.Json.JsonElement acEl)
                    ? acEl.GetInt32()
                    : 1;

                if (tenantId != Guid.Empty)
                {
                    ISubscriptionRepository subscriptions = services.GetRequiredService<ISubscriptionRepository>();
                    Billing.Domain.Entities.Subscription? subscription =
                        await subscriptions.FindByTenantAsync(tenantId, ct).ConfigureAwait(false);

                    if (subscription is not null)
                    {
                        Billing.Domain.Entities.Subscription? tracked =
                            await subscriptions.FindByIdAsync(subscription.Id, ct).ConfigureAwait(false);

                        if (tracked is not null)
                        {
                            if (isTerminal)
                            {
                                tracked.RecordTerminalPaymentFailure(providerInvoiceId);
                            }
                            else
                            {
                                tracked.RecordPaymentFailure();
                            }

                            subscriptions.Update(tracked);
                            await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);

                            logger.LogWarning(
                                "Payment failure recorded for tenant {TenantId}, invoice {InvoiceId}, terminal={IsTerminal}.",
                                tenantId,
                                providerInvoiceId,
                                isTerminal);
                        }
                    }
                }

                // Publish integration event for notification consumers (dunning email).
                InvoicePaymentFailedIntegrationEvent paymentFailedEvent = new InvoicePaymentFailedIntegrationEvent
                {
                    TenantId = tenantId,
                    ProviderInvoiceId = providerInvoiceId,
                    AmountDueCents = amountDue,
                    Currency = currency,
                    AttemptCount = attemptCount,
                    FailedAt = DateTimeOffset.UtcNow,
                    NextRetryAt = isTerminal ? null : DateTimeOffset.UtcNow.AddDays(1).ToString("O"),
                };

                await publishEndpoint.Publish(paymentFailedEvent, ct).ConfigureAwait(false);
                logger.LogInformation("InvoicePaymentFailedIntegrationEvent published for tenant {TenantId}.", tenantId);
                break;
            }

            case "customer.subscription.trial_will_end":
                logger.LogInformation("Stripe trial_will_end event received — TODO: notify tenant.");
                break;

            default:
                logger.LogDebug("Unhandled Stripe event type: {EventType}.", eventType);
                break;
        }
    }

    /// <summary>Returns the data.object element from a Stripe event, or an empty JsonElement on failure.</summary>
    private static System.Text.Json.JsonElement GetStripeDataObject(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("data", out System.Text.Json.JsonElement data)
            && data.TryGetProperty("object", out System.Text.Json.JsonElement obj))
        {
            return obj;
        }

        return default;
    }

    /// <summary>
    /// Extracts tenant_id from a Stripe event root where the data.object is a subscription.
    /// Stripe puts tenant metadata in data.object.metadata.tenant_id.
    /// </summary>
    private static Guid TryGetTenantIdFromSubscriptionObject(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("data", out System.Text.Json.JsonElement data)
            && data.TryGetProperty("object", out System.Text.Json.JsonElement obj)
            && obj.TryGetProperty("metadata", out System.Text.Json.JsonElement meta)
            && meta.TryGetProperty("tenant_id", out System.Text.Json.JsonElement tid)
            && Guid.TryParse(tid.GetString(), out Guid tenantId))
        {
            return tenantId;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Extracts tenant_id from a Stripe event root where the data.object is an invoice.
    /// Stripe invoice metadata is at data.object.subscription_details.metadata.tenant_id,
    /// with a fallback to data.object.metadata.tenant_id.
    /// </summary>
    private static Guid TryGetTenantIdFromInvoiceObject(System.Text.Json.JsonElement root)
    {
        if (root.TryGetProperty("data", out System.Text.Json.JsonElement data)
            && data.TryGetProperty("object", out System.Text.Json.JsonElement obj))
        {
            // Try subscription_details.metadata first (Stripe 2022-11-15+).
            if (obj.TryGetProperty("subscription_details", out System.Text.Json.JsonElement subDetails)
                && subDetails.TryGetProperty("metadata", out System.Text.Json.JsonElement subMeta)
                && subMeta.TryGetProperty("tenant_id", out System.Text.Json.JsonElement tid1)
                && Guid.TryParse(tid1.GetString(), out Guid tenantId1))
            {
                return tenantId1;
            }

            // Fallback: direct metadata on invoice.
            if (obj.TryGetProperty("metadata", out System.Text.Json.JsonElement meta)
                && meta.TryGetProperty("tenant_id", out System.Text.Json.JsonElement tid2)
                && Guid.TryParse(tid2.GetString(), out Guid tenantId2))
            {
                return tenantId2;
            }
        }

        return Guid.Empty;
    }

    private static object Problem(string detail) => new
    {
        Type = "https://tools.ietf.org/html/rfc7807",
        Title = "Request failed.",
        Status = (int)HttpStatusCode.BadRequest,
        Detail = detail,
    };

    /// <summary>Request body for POST checkout/session.</summary>
    public sealed class CheckoutSessionRequest
    {
        /// <summary>Gets or sets the provider price identifier.</summary>
        public string? PriceId { get; set; }

        /// <summary>Gets or sets the success redirect URL.</summary>
        public string? SuccessUrl { get; set; }

        /// <summary>Gets or sets the cancel redirect URL.</summary>
        public string? CancelUrl { get; set; }
    }

    /// <summary>Request body for POST customer-portal/session.</summary>
    public sealed class PortalSessionRequest
    {
        /// <summary>Gets or sets the return URL after portal interaction.</summary>
        public string? ReturnUrl { get; set; }
    }

    /// <summary>Request body for POST subscription:upgrade.</summary>
    public sealed class UpgradeRequest
    {
        /// <summary>Gets or sets the new provider price identifier.</summary>
        public string? NewPriceId { get; set; }
    }

    /// <summary>Request body for POST subscription:cancel.</summary>
    public sealed class CancelRequest
    {
        /// <summary>Gets or sets whether to cancel at the end of the billing period (true) or immediately (false).</summary>
        public bool AtPeriodEnd { get; set; } = true;
    }

    /// <summary>Subscription projection DTO for API responses.</summary>
    private sealed class SubscriptionDto
    {
        /// <summary>Initializes a new instance of <see cref="SubscriptionDto"/>.</summary>
        public SubscriptionDto(
            Guid id,
            Guid tenantId,
            Guid planId,
            string? providerSubscriptionId,
            string status,
            DateTimeOffset startedAt,
            DateTimeOffset? trialEndsAt,
            DateTimeOffset? canceledAt)
        {
            Id = id;
            TenantId = tenantId;
            PlanId = planId;
            ProviderSubscriptionId = providerSubscriptionId;
            Status = status;
            StartedAt = startedAt;
            TrialEndsAt = trialEndsAt;
            CanceledAt = canceledAt;
        }

        /// <summary>Gets the subscription identifier.</summary>
        public Guid Id { get; }

        /// <summary>Gets the tenant identifier.</summary>
        public Guid TenantId { get; }

        /// <summary>Gets the plan identifier.</summary>
        public Guid PlanId { get; }

        /// <summary>Gets the provider subscription identifier.</summary>
        public string? ProviderSubscriptionId { get; }

        /// <summary>Gets the subscription status.</summary>
        public string Status { get; }

        /// <summary>Gets the start date.</summary>
        public DateTimeOffset StartedAt { get; }

        /// <summary>Gets the trial end date.</summary>
        public DateTimeOffset? TrialEndsAt { get; }

        /// <summary>Gets the cancellation date.</summary>
        public DateTimeOffset? CanceledAt { get; }
    }
}
