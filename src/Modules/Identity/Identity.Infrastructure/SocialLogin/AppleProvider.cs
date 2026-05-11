using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Social;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.SocialLogin;

/// <summary>
/// Apple "Sign in with Apple" OIDC adapter.
/// Configure via <c>Identity:SocialLogin:Apple:ClientId</c> (bundle/service ID),
/// <c>:TeamId</c>, <c>:KeyId</c>, and <c>:PrivateKeyPem</c>.
/// </summary>
/// <remarks>
/// Apple uses a custom JWT client-secret generation (ES256 private key sign).
/// The full implementation requires generating a signed JWT for the client_secret
/// parameter on each token exchange — this scaffold provides the interface contract.
/// </remarks>
public sealed class AppleProvider(IConfiguration configuration) : ISocialLoginAdapter
{
    /// <inheritdoc />
    public string ProviderName => "apple";

    /// <inheritdoc />
    public Task<string> BuildAuthorizationUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        string clientId = configuration["Identity:SocialLogin:Apple:ClientId"] ?? string.Empty;
        string authUrl = $"https://appleid.apple.com/auth/authorize?" +
                         $"client_id={clientId}&redirect_uri={System.Uri.EscapeDataString(returnUrl)}" +
                         $"&response_type=code+id_token&scope=email+name&response_mode=form_post";
        return Task.FromResult(authUrl);
    }

    /// <inheritdoc />
    public Task<SocialIdentity> ExchangeCodeAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException(
            "Apple Sign In exchange must be performed via ASP.NET Core authentication middleware callback. " +
            "Requires ES256 client_secret JWT generation from Apple private key.");
    }
}
