using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Application.Handlers;
using Admin.Application.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Api.Endpoints;

/// <summary>
/// Endpoints for the second-admin approval workflow.
/// </summary>
internal static class ApprovalEndpoints
{
    internal static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/approvals/{id:guid}/approve", ApproveAsync)
            .WithName("Admin_ApproveAction")
            .WithSummary("Approves a pending high-sensitivity admin action.");

        group.MapPost("/approvals/{id:guid}/deny", DenyAsync)
            .WithName("Admin_DenyAction")
            .WithSummary("Denies a pending high-sensitivity admin action.");
    }

    private static async Task<IResult> ApproveAsync(
        Guid id,
        [FromServices] ApproveAdminActionHandler handler,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        Result<PendingAdminAction> result = await handler.ApproveAsync(id, actorId, ct).ConfigureAwait(false);

        await auditor.RecordAsync(
            actorId,
            "approval.approve",
            targetTenantId: result.IsSuccess ? result.Value!.TargetTenantId : null,
            payloadJson: $"{{\"actionId\":\"{id}\"}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new
        {
            Id = result.Value!.Id,
            Status = result.Value!.Status.ToString(),
            ApproverId = result.Value!.ApproverId,
            ResolvedAt = result.Value!.ResolvedAt,
        });
    }

    private static async Task<IResult> DenyAsync(
        Guid id,
        DenyRequest request,
        [FromServices] ApproveAdminActionHandler handler,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";

        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return Results.Problem(
                detail: "Denial reason is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        Result result = await handler.DenyAsync(id, actorId, request.Reason, ct).ConfigureAwait(false);

        await auditor.RecordAsync(
            actorId,
            "approval.deny",
            targetTenantId: null,
            payloadJson: $"{{\"actionId\":\"{id}\"}}",
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new { Message = "Action denied." });
    }

    /// <summary>Request body for the deny endpoint.</summary>
    private sealed class DenyRequest
    {
        /// <summary>Gets or sets the reason for denying the action.</summary>
        public string? Reason { get; set; }
    }
}
