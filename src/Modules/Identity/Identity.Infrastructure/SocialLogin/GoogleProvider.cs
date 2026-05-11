using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Social;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.SocialLogin;

/// <summary>
/// Google OIDC social login adapter.
/// Wires <c>Microsoft.AspNetCore.Authentication.Google</c>.
/// Configure via <c>Identity:SocialLogin:Google:ClientId</c> and <c>:ClientSecret</c>.
/// </summary>
/// <remarks>
/// Full implementation requires redirect-URI round-trip through the ASP.NET Core
/// authentication middleware (Challenge → callback). This scaffold exposes the
/// interface contract; the actual OAuth2 exchange is handled by
/// <c>HttpContext.AuthenticateAsync("Google")</c> in the endpoint.
/// </remarks>
public sealed class GoogleProvider(IConfiguration configuration) : ISocialLoginAdapter
{
    /// <inheritdoc />
    public string ProviderName => "google";

    /// <inheritdoc />
    public Task<string> BuildAuthorizationUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        // The actual redirect is handled by ASP.NET Core Challenge; this method
        // is a hook for adapters that build the URL manually (e.g., server-side flows).
        string clientId = configuration["Identity:SocialLogin:Google:ClientId"] ?? string.Empty;
        string authUrl = $"https://accounts.google.com/o/oauth2/auth?" +
                         $"client_id={clientId}&redirect_uri={System.Uri.EscapeDataString(returnUrl)}" +
                         $"&response_type=code&scope=openid+email+profile";
        return Task.FromResult(authUrl);
    }

    /// <inheritdoc />
    public Task<SocialIdentity> ExchangeCodeAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        // Exchange is performed by the ASP.NET Core Google middleware via the
        // authentication callback. This scaffold delegates to that middleware.
        // Full implementation: POST to https://oauth2.googleapis.com/token, extract id_token.
        throw new System.NotImplementedException(
            "Google OIDC exchange must be performed via ASP.NET Core authentication middleware callback.");
    }
}
