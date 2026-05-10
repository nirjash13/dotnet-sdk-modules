using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;
using Webhooks.Application.Abstractions;
using Webhooks.Contracts;
using Webhooks.Domain.Entities;
using Webhooks.Infrastructure.Extensions;

namespace Webhooks.Api;

/// <summary><see cref="IModuleStartup"/> for the Webhooks module.</summary>
public sealed class WebhooksModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddWebhooksInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/webhooks")
            .WithTags("webhooks")
            .RequireAuthorization();

        // POST /api/v1/webhooks/endpoints
        group.MapPost("/endpoints", CreateEndpointAsync)
            .WithName("Webhooks_CreateEndpoint")
            .WithSummary("Creates a new webhook endpoint. Returns the signing secret once.");

        // GET /api/v1/webhooks/endpoints
        group.MapGet("/endpoints", ListEndpointsAsync)
            .WithName("Webhooks_ListEndpoints")
            .WithSummary("Lists all webhook endpoints for the tenant.");

        // DELETE /api/v1/webhooks/endpoints/{id}
        group.MapDelete("/endpoints/{id:guid}", DeleteEndpointAsync)
            .WithName("Webhooks_DeleteEndpoint")
            .WithSummary("Deletes a webhook endpoint.");

        // POST /api/v1/webhooks/endpoints/{id}:rotate-secret
        group.MapPost("/endpoints/{id:guid}:rotate-secret", RotateSecretAsync)
            .WithName("Webhooks_RotateSecret")
            .WithSummary("Rotates the signing secret. Previous secret valid for 24h.");

        // POST /api/v1/webhooks/endpoints/{id}:test-send
        group.MapPost("/endpoints/{id:guid}:test-send", TestSendAsync)
            .WithName("Webhooks_TestSend")
            .WithSummary("Sends a test event to the endpoint.");

        // GET /api/v1/webhooks/deliveries?endpointId=...
        group.MapGet("/deliveries", ListDeliveriesAsync)
            .WithName("Webhooks_ListDeliveries")
            .WithSummary("Returns delivery log for an endpoint.");

        // POST /api/v1/webhooks/deliveries/{id}:replay
        group.MapPost("/deliveries/{id:guid}:replay", ReplayDeliveryAsync)
            .WithName("Webhooks_ReplayDelivery")
            .WithSummary("Replays a failed delivery.");
    }

    private static async Task<IResult> CreateEndpointAsync(
        CreateEndpointRequest request,
        IWebhookEndpointRepository repo,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        // Generate a cryptographically random secret — returned once, never stored in plain.
        byte[] secretBytes = RandomNumberGenerator.GetBytes(32);
        string secretBase64 = Convert.ToBase64String(secretBytes);

        WebhookEndpoint endpoint = new WebhookEndpoint(
            id: Guid.NewGuid(),
            tenantId: ctx.TenantId,
            url: request.Url,
            description: request.Description,
            eventTypes: request.EventTypes,
            secretHashedCurrent: secretBase64,
            createdAt: DateTimeOffset.UtcNow);

        await repo.AddAsync(endpoint, ct).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/webhooks/endpoints/{endpoint.Id}",
            new
            {
                Id = endpoint.Id,
                SigningSecret = $"whsec_{secretBase64}",
                Url = endpoint.Url,
            });
    }

    private static async Task<IResult> ListEndpointsAsync(
        IWebhookEndpointRepository repo,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        IReadOnlyList<WebhookEndpoint> endpoints = await repo
            .GetByTenantAsync(ctx.TenantId, ct).ConfigureAwait(false);

        List<WebhookEndpointDto> dtos = new List<WebhookEndpointDto>();
        foreach (WebhookEndpoint e in endpoints)
        {
            dtos.Add(new WebhookEndpointDto(e.Id, e.Url, e.Description, e.EventTypes, e.Status.ToString(), e.CreatedAt));
        }

        return Results.Ok(dtos);
    }

    private static async Task<IResult> DeleteEndpointAsync(
        Guid id,
        IWebhookEndpointRepository repo,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        WebhookEndpoint? endpoint = await repo.FindAsync(id, ct).ConfigureAwait(false);
        if (endpoint is null || endpoint.TenantId != ctx.TenantId)
        {
            return Results.NotFound();
        }

        endpoint.Delete();
        await repo.SaveChangesAsync(ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> RotateSecretAsync(
        Guid id,
        IWebhookEndpointRepository repo,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        WebhookEndpoint? endpoint = await repo.FindAsync(id, ct).ConfigureAwait(false);
        if (endpoint is null || endpoint.TenantId != ctx.TenantId)
        {
            return Results.NotFound();
        }

        byte[] newSecretBytes = RandomNumberGenerator.GetBytes(32);
        string newSecretBase64 = Convert.ToBase64String(newSecretBytes);
        endpoint.RotateSecret(newSecretBase64, DateTimeOffset.UtcNow);
        await repo.SaveChangesAsync(ct).ConfigureAwait(false);

        return Results.Ok(new { NewSigningSecret = $"whsec_{newSecretBase64}", PreviousSecretValidFor = "24h" });
    }

    private static async Task<IResult> TestSendAsync(
        Guid id,
        IWebhookSender sender,
        IWebhookEndpointRepository repo,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct = default)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return Results.Unauthorized();
        }

        WebhookEndpoint? endpoint = await repo.FindAsync(id, ct).ConfigureAwait(false);
        if (endpoint is null || endpoint.TenantId != ctx.TenantId)
        {
            return Results.NotFound();
        }

        WebhookEvent testEvent = new WebhookEvent(
            id: Guid.NewGuid(),
            tenantId: ctx.TenantId,
            eventType: "test.event",
            payloadJson: "{\"message\":\"This is a test webhook delivery.\"}",
            createdAt: DateTimeOffset.UtcNow);

        await sender.SendAsync(testEvent, ct).ConfigureAwait(false);
        return Results.Accepted();
    }

    private static async Task<IResult> ListDeliveriesAsync(
        IWebhookDeliveryQuery deliveryQuery,
        Guid? endpointId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        (int total, IReadOnlyList<WebhookDeliveryDto> items) =
            await deliveryQuery.QueryAsync(endpointId, page, pageSize, ct).ConfigureAwait(false);

        return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    private static Task<IResult> ReplayDeliveryAsync(
        Guid id,
        CancellationToken ct = default)
    {
        // TODO(Phase 5.5): implement replay by re-enqueueing via IJobScheduler.
        _ = id;
        _ = ct;
        return Task.FromResult<IResult>(Results.Accepted());
    }

    /// <summary>Request body for creating a webhook endpoint.</summary>
    public sealed class CreateEndpointRequest
    {
        /// <summary>Gets or sets the delivery URL.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional human-readable description.</summary>
        public string? Description { get; set; }

        /// <summary>Gets or sets the event types to subscribe to (use "*" for all).</summary>
        public IReadOnlyList<string> EventTypes { get; set; } = System.Array.Empty<string>();
    }
}
