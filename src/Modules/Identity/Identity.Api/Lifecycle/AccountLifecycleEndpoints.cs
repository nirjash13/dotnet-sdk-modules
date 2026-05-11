using System;
using System.Security.Claims;
using Identity.Application.Lifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Identity.Api.Lifecycle;

/// <summary>
/// Minimal API endpoints for account lifecycle management (Phase 2.11).
/// </summary>
public static class AccountLifecycleEndpoints
{
    /// <summary>Maps account deletion and restore endpoints onto the given route builder.</summary>
    public static void MapAccountLifecycleEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder identity = endpoints
            .MapGroup("/api/v1/identity")
            .WithTags("account-lifecycle");

        // DELETE /api/v1/identity/me — initiates soft-deletion of the authenticated account.
        identity.MapDelete("me", RequestAccountDeletion)
            .WithName("Account_RequestDeletion")
            .WithSummary("Soft-deletes the authenticated user's account with a 30-day restore window.")
            .RequireAuthorization();

        // POST /api/v1/identity/account/restore — restores a soft-deleted account via token.
        identity.MapPost("account/restore", RestoreAccount)
            .WithName("Account_Restore")
            .WithSummary("Restores a soft-deleted account using the single-use token from the deletion email.")
            .AllowAnonymous();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> RequestAccountDeletion(
        ClaimsPrincipal user,
        RequestAccountDeletionHandler handler,
        IConfiguration configuration,
        HttpContext context)
    {
        Guid userId = GetUserId(user);
        if (userId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        string restoreBaseUrl = configuration["Identity:AccountRestoreBaseUrl"]
            ?? $"{context.Request.Scheme}://{context.Request.Host}/account/restore";

        RequestAccountDeletionCommand command = new RequestAccountDeletionCommand(
            UserId: userId,
            RestoreBaseUrl: restoreBaseUrl);

        SaasBuilder.SharedKernel.Abstractions.Result result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Account deletion request failed");
        }

        return Results.Accepted();
    }

    private static async Task<IResult> RestoreAccount(
        [FromBody] RestoreAccountRequest request,
        RestoreAccountHandler handler,
        HttpContext context)
    {
        RestoreAccountCommand command = new RestoreAccountCommand(RawToken: request.Token);

        SaasBuilder.SharedKernel.Abstractions.Result result =
            await handler.HandleAsync(command, context.RequestAborted);

        if (!result.IsSuccess)
        {
            return Results.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Account restore failed");
        }

        return Results.Ok();
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    /// <summary>Request body for account restore.</summary>
    /// <param name="Token">The raw restore token from the deletion email link.</param>
    public sealed record RestoreAccountRequest(string Token);

    // ── Claim helpers ─────────────────────────────────────────────────────────

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        string? raw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return raw is not null && Guid.TryParse(raw, out Guid id) ? id : Guid.Empty;
    }
}
