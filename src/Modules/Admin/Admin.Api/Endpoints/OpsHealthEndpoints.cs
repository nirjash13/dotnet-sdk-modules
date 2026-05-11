using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Admin.Api.Endpoints;

/// <summary>
/// Operational health endpoints for the admin control plane.
/// </summary>
internal static class OpsHealthEndpoints
{
    internal static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/ops/health", GetOpsHealthAsync)
            .WithName("Admin_OpsHealth")
            .WithSummary("Returns the aggregated operational health of DB, queue, providers, and SLO.");
    }

    private static async Task<IResult> GetOpsHealthAsync(
        [FromServices] IOpsHealthChecker checker,
        CancellationToken ct = default)
    {
        OpsHealthDto dto = await checker.CheckAsync(ct).ConfigureAwait(false);

        int statusCode = dto.Overall switch
        {
            ComponentStatus.Healthy => StatusCodes.Status200OK,
            ComponentStatus.Degraded => StatusCodes.Status200OK,
            _ => StatusCodes.Status503ServiceUnavailable,
        };

        return Results.Json(dto, statusCode: statusCode);
    }
}
