using System;
using System.Security.Claims;
using Identity.Application.Social;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.SocialLogin;

/// <summary>
/// Minimal API endpoints for social login and account linking (Phase 2 scaffold).
/// Social login proper is handled by ASP.NET Core authentication middleware callbacks;
/// these endpoints provide the account-linking flow for existing authenticated users.
/// </summary>
public static class SocialLoginEndpoints
{
    /// <summary>Maps social login and account-linking endpoints.</summary>
    public static void MapSocialLoginEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder social = endpoints
            .MapGroup("/api/v1/identity/social")
            .WithTags("identity-social");

        // Account linking — authenticated user adds a social provider to their account.
        social.MapPost("link", LinkSocialProvider)
            .WithName("Identity_Social_Link")
            .WithSummary("Links a social login provider to the current user's account.")
            .RequireAuthorization();

        // Authorization URL — initiates the social login redirect.
        social.MapGet("{provider}/authorize", GetAuthorizationUrl)
            .WithName("Identity_Social_GetAuthUrl")
            .WithSummary("Returns the OAuth2/OIDC authorization URL for the given provider.")
            .AllowAnonymous();
    }

    private static async Task<IResult> GetAuthorizationUrl(
        string provider,
        string? returnUrl,
        IEnumerable<ISocialLoginAdapter> adapters,
        CancellationToken ct)
    {
        ISocialLoginAdapter? adapter = adapters.FirstOrDefault(a =>
            a.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (adapter is null)
        {
            return Results.NotFound(new { code = "PROVIDER_NOT_FOUND", detail = $"Provider '{provider}' is not configured." });
        }

        string url = await adapter.BuildAuthorizationUrlAsync(returnUrl ?? "/", ct);
        return Results.Ok(new { authorizationUrl = url });
    }

    private static Task<IResult> LinkSocialProvider(
        LinkSocialProviderRequest request,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // Full implementation: validate the OAuth2 callback code, look up the external identity,
        // link it to the current user's account. Phase 2 scaffold returns 501.
        return Task.FromResult(Results.StatusCode(501));
    }

    /// <summary>Request body for linking a social provider.</summary>
    public sealed record LinkSocialProviderRequest(string? Provider, string? Code, string? State);
}
