using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Contracts;
using Billing.Infrastructure.Extensions;
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

        // GET /api/v1/billing/subscription
        billing.MapGet("/subscription", GetSubscriptionAsync)
            .WithName("Billing_GetSubscription")
            .WithSummary("Returns the current tenant's active subscription.");

        // POST /api/v1/billing/subscription:upgrade
        billing.MapPost("/subscription:upgrade", UpgradeSubscriptionAsync)
            .WithName("Billing_UpgradeSubscription")
            .WithSummary("Upgrades the current subscription to a new plan.");

        // POST /api/v1/billing/subscription:cancel
        billing.MapPost("/subscription:cancel", CancelSubscriptionAsync)
            .WithName("Billing_CancelSubscription")
            .WithSummary("Cancels the current subscription.");

        // GET /api/v1/billing/invoices
        billing.MapGet("/invoices", GetInvoicesAsync)
            .WithName("Billing_GetInvoices")
            .WithSummary("Returns the paginated invoice list for the current tenant.");

        // POST /api/v1/billing/portal/session
        billing.MapPost("/portal/session", CreatePortalSessionAsync)
            .WithName("Billing_CreatePortalSession")
            .WithSummary("Creates a customer portal session and returns the provider redirect URL.");

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

        // TODO(Phase 4): Validate successUrl/cancelUrl; use configured base URL as fallback.
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

    private static IResult GetSubscriptionAsync()
    {
        // TODO(Phase 4): Resolve ISubscriptionRepository and project to SubscriptionDto.
        return Results.Problem(
            detail: "TODO(Phase 4): GetSubscription not yet implemented.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static IResult UpgradeSubscriptionAsync()
    {
        // TODO(Phase 4): Parse request, resolve handler, execute.
        return Results.Problem(
            detail: "TODO(Phase 4): UpgradeSubscription not yet implemented.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static IResult CancelSubscriptionAsync()
    {
        // TODO(Phase 4): Parse request, resolve handler, execute.
        return Results.Problem(
            detail: "TODO(Phase 4): CancelSubscription not yet implemented.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static IResult GetInvoicesAsync()
    {
        // TODO(Phase 4): Fetch invoice list from IBillingProvider or local cache.
        return Results.Problem(
            detail: "TODO(Phase 4): GetInvoices not yet implemented.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static IResult CreatePortalSessionAsync()
    {
        // TODO(Phase 4): Create customer portal session via IBillingProvider.
        return Results.Problem(
            detail: "TODO(Phase 4): CreatePortalSession not yet implemented.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static async Task<IResult> ReceiveWebhookAsync(
        string provider,
        HttpRequest httpRequest,
        IServiceProvider services,
        IWebhookEventRepository webhookEvents,
        ILogger<BillingModule> logger,
        CancellationToken ct)
    {
        // 1. Read raw body (must be done before model binding consumes it).
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
        string signatureHeader = httpRequest.Headers["Stripe-Signature"].ToString()
            ?? httpRequest.Headers["X-Signature"].ToString()
            ?? string.Empty;

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

        // 5. Record the event for idempotency.
        await webhookEvents.RecordAsync(idempotencyKey, verification.EventType!, ct).ConfigureAwait(false);

        // 6. TODO(Phase 4): Dispatch domain event based on verification.EventType.
        logger.LogInformation(
            "Webhook received from {Provider}: eventType={EventType}, key={Key}. TODO(Phase 4): dispatch domain event.",
            provider,
            verification.EventType,
            idempotencyKey);

        return Results.Ok(new { Message = "Accepted." });
    }

    private static object Problem(string detail) => new
    {
        Type = "https://tools.ietf.org/html/rfc7807",
        Title = "Request failed.",
        Status = (int)HttpStatusCode.BadRequest,
        Detail = detail,
    };

    /// <summary>Request body for the POST checkout/session endpoint.</summary>
    public sealed class CheckoutSessionRequest
    {
        /// <summary>Gets or sets the provider price identifier to subscribe to.</summary>
        public string? PriceId { get; set; }

        /// <summary>Gets or sets the URL to redirect to on successful payment.</summary>
        public string? SuccessUrl { get; set; }

        /// <summary>Gets or sets the URL to redirect to if the customer cancels.</summary>
        public string? CancelUrl { get; set; }
    }
}
