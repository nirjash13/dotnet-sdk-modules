using System;
using System.Security.Claims;
using Identity.Application.Organizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Organizations;

/// <summary>
/// Minimal API endpoints for organization domain-claim management (Phase 2.4).
/// All endpoints require authentication; the requesting user must be an org owner.
/// </summary>
public static class DomainClaimEndpoints
{
    /// <summary>Maps domain-claim endpoints onto the given route builder.</summary>
    public static void MapDomainClaimEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/organizations/{orgId:guid}/domain-claims")
            .RequireAuthorization()
            .WithTags("organization-domain-claims");

        group.MapPost(string.Empty, ClaimDomain)
            .WithName("DomainClaims_Claim")
            .WithSummary("Claim a domain for an organization. Returns the DNS TXT verification token.");

        group.MapPost("{claimId:guid}:verify", VerifyDomainClaim)
            .WithName("DomainClaims_Verify")
            .WithSummary("Trigger DNS TXT verification for a pending domain claim.");

        group.MapDelete("{claimId:guid}", DeleteDomainClaim)
            .WithName("DomainClaims_Delete")
            .WithSummary("Delete a domain claim.");
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ClaimDomain(
        Guid orgId,
        [FromBody] ClaimDomainRequest request,
        ClaimsPrincipal user,
        ClaimDomainHandler handler,
        HttpContext context)
    {
        Guid userId = GetUserId(user);

        ClaimDomainCommand command = new ClaimDomainCommand(
            OrganizationId: orgId,
            Domain: request.Domain,
            RequestingUserId: userId);

        SaasBuilder.SharedKernel.Abstractions.Result<ClaimDomainResult> result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status409Conflict,
                title: "Domain claim failed");
        }

        return Results.Created(
            $"/api/v1/organizations/{orgId}/domain-claims/{result.Value!.ClaimId}",
            new ClaimDomainResponse(result.Value.ClaimId, result.Value.VerificationToken));
    }

    private static async Task<IResult> VerifyDomainClaim(
        Guid orgId,
        Guid claimId,
        VerifyDomainClaimHandler handler,
        HttpContext context)
    {
        VerifyDomainClaimCommand command = new VerifyDomainClaimCommand(
            OrganizationId: orgId,
            ClaimId: claimId);

        SaasBuilder.SharedKernel.Abstractions.Result result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Domain verification failed");
        }

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDomainClaim(
        Guid orgId,
        Guid claimId,
        DeleteDomainClaimHandler handler,
        HttpContext context)
    {
        DeleteDomainClaimCommand command = new DeleteDomainClaimCommand(
            OrganizationId: orgId,
            ClaimId: claimId);

        SaasBuilder.SharedKernel.Abstractions.Result result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    /// <summary>Request body for claiming a domain.</summary>
    /// <param name="Domain">The domain to claim (e.g., "acme.com").</param>
    public sealed record ClaimDomainRequest(string Domain);

    /// <summary>Response returned when a domain is successfully claimed.</summary>
    /// <param name="ClaimId">The new claim identifier.</param>
    /// <param name="VerificationToken">DNS TXT token to publish under <c>_saasbuilder-verify.&lt;domain&gt;</c>.</param>
    public sealed record ClaimDomainResponse(Guid ClaimId, string VerificationToken);

    // ── Claim helpers ─────────────────────────────────────────────────────────

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        string? raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return raw is not null && Guid.TryParse(raw, out Guid id) ? id : Guid.Empty;
    }
}
