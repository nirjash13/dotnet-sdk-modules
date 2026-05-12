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

    private static Task<IResult> StartImpersonation(
        StartImpersonationRequest request,
        IImpersonationService impersonationService,
        CancellationToken ct)
    {
        // TODO(C-12): wire AddJwtBearer impersonation scheme before re-enabling.
        // The minted impersonation token has no registered JwtBearer validation scheme —
        // callers cannot use it. Endpoint is disabled until the scheme is wired.
        return Task.FromResult(Results.StatusCode(StatusCodes.Status501NotImplemented));
    }

    private static Task<IResult> EndImpersonation(
        EndImpersonationRequest request,
        IImpersonationService impersonationService,
        CancellationToken ct)
    {
        // TODO(C-12): wire AddJwtBearer impersonation scheme before re-enabling.
        return Task.FromResult(Results.StatusCode(StatusCodes.Status501NotImplemented));
    }

    /// <summary>Request body for starting impersonation.</summary>
    public sealed record StartImpersonationRequest(Guid TargetUserId, string? Reason);

    /// <summary>Request body for ending impersonation.</summary>
    public sealed record EndImpersonationRequest(Guid SessionId);
}
