using System;
using System.Security.Claims;
using Identity.Application.Mfa;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.Mfa;

/// <summary>
/// Minimal API endpoints for TOTP MFA enrollment and verification (Phase 2).
/// </summary>
public static class MfaEndpoints
{
    /// <summary>Maps TOTP MFA endpoints.</summary>
    public static void MapMfaEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder mfa = endpoints
            .MapGroup("/api/v1/identity/mfa")
            .RequireAuthorization()
            .WithTags("identity-mfa");

        mfa.MapPost("totp/enroll", EnrollTotp)
            .WithName("Identity_Mfa_TotpEnroll")
            .WithSummary("Starts TOTP enrollment. Returns the secret and QR code data URL.");

        mfa.MapPost("totp/verify", VerifyTotp)
            .WithName("Identity_Mfa_TotpVerify")
            .WithSummary("Verifies a TOTP code. Confirms enrollment on first success.");
    }

    private static async Task<IResult> EnrollTotp(
        ClaimsPrincipal user,
        ITotpService totpService,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out Guid userId))
        {
            return Results.Unauthorized();
        }

        TotpEnrollmentResult result = await totpService.EnrollAsync(userId, ct);

        return Results.Ok(new
        {
            secret = result.Secret,
            qrCodeDataUrl = result.QrCodeUri,
        });
    }

    private static async Task<IResult> VerifyTotp(
        TotpVerifyRequest request,
        ClaimsPrincipal user,
        ITotpService totpService,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out Guid userId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.BadRequest(new { code = "INVALID_CODE", detail = "TOTP code is required." });
        }

        bool valid = await totpService.VerifyAsync(userId, request.Code, ct);
        return valid
            ? Results.Ok(new { verified = true })
            : Results.BadRequest(new { code = "INVALID_CODE", detail = "TOTP code is invalid or expired." });
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        string? sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    /// <summary>Request body for TOTP verification.</summary>
    public sealed record TotpVerifyRequest(string? Code);
}
