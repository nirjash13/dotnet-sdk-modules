using System;
using System.Security.Claims;
using Identity.Application.Impersonation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Impersonation;

/// <summary>
/// Minimal API endpoints for admin impersonation (Phase 2 — safe impersonation pattern).
/// All endpoints require the caller to be authenticated; start also requires system-admin role.
/// </summary>
public static class ImpersonationEndpoints
{
    /// <summary>Maps impersonation start/end endpoints.</summary>
    public static void MapImpersonationEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder impersonation = endpoints
            .MapGroup("/api/v1/identity/impersonation")
            .RequireAuthorization()
            .WithTags("identity-impersonation");

        impersonation.MapPost("start", StartImpersonation)
            .WithName("Identity_Impersonation_Start")
            .WithSummary("Starts an impersonation session. System-admin only. Requires a mandatory reason.")
            .RequireAuthorization(policy => policy.RequireRole("system.admin"));

        impersonation.MapPost("end", EndImpersonation)
            .WithName("Identity_Impersonation_End")
            .WithSummary("Ends the current impersonation session.");
    }

    private static async Task<IResult> StartImpersonation(
        StartImpersonationRequest request,
        ClaimsPrincipal user,
        IImpersonationService impersonationService,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out Guid adminUserId))
        {
            return Results.Unauthorized();
        }

        if (request.TargetUserId == Guid.Empty)
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", detail = "TargetUserId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", detail = "Reason is required for impersonation." });
        }

        ImpersonationSession session = await impersonationService.StartAsync(
            adminUserId,
            request.TargetUserId,
            request.Reason,
            ct);

        return Results.Ok(new
        {
            sessionId = session.SessionId,
            impersonationToken = session.ImpersonationToken,
            expiresAt = session.ExpiresAt,
            warning = "This session is audited. Use responsibly.",
        });
    }

    private static async Task<IResult> EndImpersonation(
        EndImpersonationRequest request,
        IImpersonationService impersonationService,
        CancellationToken ct)
    {
        if (request.SessionId == Guid.Empty)
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", detail = "SessionId is required." });
        }

        await impersonationService.EndAsync(request.SessionId, ct);
        return Results.NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        string? sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    /// <summary>Request body for starting impersonation.</summary>
    public sealed record StartImpersonationRequest(Guid TargetUserId, string? Reason);

    /// <summary>Request body for ending impersonation.</summary>
    public sealed record EndImpersonationRequest(Guid SessionId);
}
