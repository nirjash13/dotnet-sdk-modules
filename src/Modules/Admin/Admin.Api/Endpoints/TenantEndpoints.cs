using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Application.Handlers;
using Admin.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Api.Endpoints;

/// <summary>
/// Tenant directory and inspector endpoints for the admin control plane.
/// </summary>
internal static class TenantEndpoints
{
    internal static void Map(IEndpointRouteBuilder group)
    {
        // GET /api/v1/admin/tenants
        group.MapGet("/tenants", ListTenantsAsync)
            .WithName("Admin_ListTenants")
            .WithSummary("Returns a paginated directory of all tenants. Optionally filter by slug/name and status.");

        // GET /api/v1/admin/tenants/{id}
        group.MapGet("/tenants/{id:guid}", GetTenantInspectorAsync)
            .WithName("Admin_GetTenantInspector")
            .WithSummary("Returns the full inspector view for a single tenant.");
    }

    private static async Task<IResult> ListTenantsAsync(
        [FromServices] ListTenantsHandler handler,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";
        await auditor.RecordAsync(
            actorId,
            "tenant.list",
            targetTenantId: null,
            payloadJson: JsonSerializer.Serialize(new { search, status, page }),
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        Result<(int Total, System.Collections.Generic.IReadOnlyList<TenantSummaryDto> Items)> result =
            await handler.HandleAsync(search, status, page, pageSize, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new
        {
            Total = result.Value!.Total,
            Page = page,
            PageSize = pageSize,
            Items = result.Value!.Items,
        });
    }

    private static async Task<IResult> GetTenantInspectorAsync(
        Guid id,
        [FromServices] GetTenantInspectorHandler handler,
        [FromServices] IAdminActionAuditor auditor,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        string actorId = httpContext.User.FindFirstValue("sub") ?? "unknown";
        await auditor.RecordAsync(
            actorId,
            "tenant.inspect",
            targetTenantId: id,
            payloadJson: null,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            httpContext.Request.Headers.UserAgent.ToString(),
            ct).ConfigureAwait(false);

        Result<TenantInspectorDto> result = await handler.HandleAsync(id, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Results.Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(result.Value);
    }
}
