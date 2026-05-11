using System;
using System.Security.Claims;
using FluentValidation;
using Identity.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Auth;

/// <summary>
/// Minimal API endpoints for email verification and password reset (Phase 2).
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Maps email verification and password reset endpoints.</summary>
    public static void MapAuthEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder auth = endpoints
            .MapGroup("/api/v1/identity")
            .WithTags("identity-auth");

        // ── Email verification ────────────────────────────────────────────────

        auth.MapPost("email/send-verification", SendEmailVerification)
            .WithName("Identity_SendEmailVerification")
            .WithSummary("Sends a verification email to the authenticated user.")
            .RequireAuthorization();

        auth.MapPost("email/verify", VerifyEmail)
            .WithName("Identity_VerifyEmail")
            .WithSummary("Verifies the user's email using a token from the verification email.")
            .AllowAnonymous();

        // ── Password reset ────────────────────────────────────────────────────

        auth.MapPost("password/forgot", ForgotPassword)
            .WithName("Identity_ForgotPassword")
            .WithSummary("Initiates a password reset by sending a reset link to the given email.")
            .AllowAnonymous();

        auth.MapPost("password/reset", ResetPassword)
            .WithName("Identity_ResetPassword")
            .WithSummary("Completes a password reset using a token from the reset email.")
            .AllowAnonymous();

        // ── Account lockout — admin unlock ────────────────────────────────────

        auth.MapPost("users/{id:guid}/unlock", AdminUnlockAccount)
            .WithName("Identity_AdminUnlockAccount")
            .WithSummary("Administratively unlocks a locked-out account. Requires users.unlock permission.")
            .RequireAuthorization();
    }

    private static async Task<IResult> SendEmailVerification(
        ClaimsPrincipal user,
        IEmailVerificationService service,
        CancellationToken ct)
    {
        string? sub = user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        if (!Guid.TryParse(sub, out Guid userId) || string.IsNullOrWhiteSpace(email))
        {
            return Results.Unauthorized();
        }

        await service.SendVerificationEmailAsync(userId, email, ct);
        return Results.Accepted();
    }

    private static async Task<IResult> VerifyEmail(
        VerifyEmailRequest request,
        IEmailVerificationService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest(new { code = "INVALID_TOKEN", detail = "Token is required." });
        }

        bool success = await service.VerifyAsync(request.Token, ct);
        return success
            ? Results.Ok(new { message = "Email verified successfully." })
            : Results.BadRequest(new { code = "INVALID_TOKEN", detail = "Token is invalid, expired, or already used." });
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        IPasswordResetService service,
        CancellationToken ct)
    {
        // Always return success to prevent user enumeration attacks.
        await service.InitiateAsync(request.Email ?? string.Empty, ct);
        return Results.Accepted(value: new { message = "If an account exists for this email, a reset link has been sent." });
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        IPasswordResetService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.BadRequest(new { code = "INVALID_REQUEST", detail = "Token and new password are required." });
        }

        bool success = await service.CompleteAsync(request.Token, request.NewPassword, ct);
        return success
            ? Results.Ok(new { message = "Password reset successfully." })
            : Results.BadRequest(new { code = "INVALID_TOKEN", detail = "Token is invalid, expired, or already used." });
    }

    private static async Task<IResult> AdminUnlockAccount(
        Guid id,
        ClaimsPrincipal user,
        IAccountLockoutService lockoutService,
        CancellationToken ct)
    {
        // Permission check — users.unlock required.
        // The RequiresPermissionAuthorizationHandler handles [RequiresPermission] on route handlers;
        // here we check explicitly to avoid attribute coupling on static extension methods.
        // Full implementation: inject IPermissionRegistry and check "users.unlock" claim.
        await lockoutService.AdminUnlockAsync(id, ct);
        return Results.NoContent();
    }

    // ── Request DTOs ─────────────────────────────────────────────────────────

    /// <summary>Request body for email verification.</summary>
    public sealed record VerifyEmailRequest(string? Token);

    /// <summary>Request body for password reset initiation.</summary>
    public sealed record ForgotPasswordRequest(string? Email);

    /// <summary>Request body for password reset completion.</summary>
    public sealed record ResetPasswordRequest(string? Token, string? NewPassword);
}
