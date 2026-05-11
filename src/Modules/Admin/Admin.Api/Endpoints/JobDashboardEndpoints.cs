using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Admin.Api.Endpoints;

/// <summary>
/// Admin endpoints for inspecting and replaying the dead-letter queue.
/// </summary>
internal static class JobDashboardEndpoints
{
    internal static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/jobs/dlq", ListDlqAsync)
            .WithName("Admin_ListDlq")
            .WithSummary("Returns dead-lettered jobs since the specified timestamp.");

        group.MapPost("/jobs/dlq/{id:guid}/replay", ReplayDlqJobAsync)
            .WithName("Admin_ReplayDlqJob")
            .WithSummary("Replays a dead-lettered job.");
    }

    private static async Task<IResult> ListDlqAsync(
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        DateTimeOffset? since = null,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        await auditor.RecordAsync(
            actorId,
            "jobs.dlq.list",
            targetTenantId: null,
            payloadJson: $"{{\"since\":\"{since}\"}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        // TODO(Phase 6.x): Add IDeadLetterQueueStore.ListAsync(since, page, pageSize, ct).
        // The current IDeadLetterQueueStore interface only exposes AddAsync — extend it for listing.
        await Task.CompletedTask.ConfigureAwait(false);
        return Results.Problem(
            detail: "IDeadLetterQueueStore.ListAsync is not yet implemented. Extend the interface to support listing.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }

    private static async Task<IResult> ReplayDlqJobAsync(
        Guid id,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        await auditor.RecordAsync(
            actorId,
            "jobs.dlq.replay",
            targetTenantId: null,
            payloadJson: $"{{\"jobId\":\"{id}\"}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        // TODO(Phase 6.x): Add IDeadLetterQueueStore.ReplayAsync(id, ct).
        await Task.CompletedTask.ConfigureAwait(false);
        return Results.Problem(
            detail: "IDeadLetterQueueStore.ReplayAsync is not yet implemented. Extend the interface to support replay.",
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Not implemented");
    }
}
