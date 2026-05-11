using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SaasBuilder.SharedKernel.Abstractions;
using Webhooks.Application.Abstractions;

namespace Admin.Api.Endpoints;

/// <summary>
/// Admin endpoints for the webhook delivery dashboard.
/// </summary>
internal static class WebhookDashboardEndpoints
{
    internal static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/webhooks/deliveries", ListDeliveriesAsync)
            .WithName("Admin_ListWebhookDeliveries")
            .WithSummary("Returns paginated webhook delivery attempts, optionally filtered by status and endpoint.");

        group.MapPost("/webhooks/deliveries/{id:guid}/replay", ReplayDeliveryAsync)
            .WithName("Admin_ReplayWebhookDelivery")
            .WithSummary("Replays a webhook delivery attempt.");
    }

    private static async Task<IResult> ListDeliveriesAsync(
        [FromServices] IWebhookDeliveryQuery deliveryQuery,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        Guid? endpointId = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        await auditor.RecordAsync(
            actorId,
            "webhooks.deliveries.list",
            targetTenantId: null,
            payloadJson: $"{{\"endpointId\":\"{endpointId}\",\"status\":\"{status}\",\"page\":{page}}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        (int total, var items) = await deliveryQuery
            .QueryAsync(endpointId, page, pageSize, ct)
            .ConfigureAwait(false);

        return Results.Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    private static async Task<IResult> ReplayDeliveryAsync(
        Guid id,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        await auditor.RecordAsync(
            actorId,
            "webhooks.delivery.replay",
            targetTenantId: null,
            payloadJson: $"{{\"deliveryId\":\"{id}\"}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        // TODO(Phase 6.x): IWebhookSender doesn't expose a ReplayAsync(deliveryId) method yet.
        // Extend IWebhookSender to support replay from existing delivery attempt by ID.
        await Task.CompletedTask.ConfigureAwait(false);
        return Results.Problem(
            detail: "IWebhookSender.ReplayAsync is not yet implemented. Extend the interface to support replay by delivery ID.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }
}
