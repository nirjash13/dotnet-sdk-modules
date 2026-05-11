using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Social;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.SocialLogin;

/// <summary>
/// GitHub OAuth2 social login adapter.
/// Wires <c>AspNet.Security.OAuth.GitHub</c>.
/// Configure via <c>Identity:SocialLogin:GitHub:ClientId</c> and <c>:ClientSecret</c>.
/// </summary>
public sealed class GitHubProvider(IConfiguration configuration) : ISocialLoginAdapter
{
    /// <inheritdoc />
    public string ProviderName => "github";

    /// <inheritdoc />
    public Task<string> BuildAuthorizationUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        string clientId = configuration["Identity:SocialLogin:GitHub:ClientId"] ?? string.Empty;
        string authUrl = $"https://github.com/login/oauth/authorize?" +
                         $"client_id={clientId}&redirect_uri={System.Uri.EscapeDataString(returnUrl)}" +
                         $"&scope=user:email";
        return Task.FromResult(authUrl);
    }

    /// <inheritdoc />
    public Task<SocialIdentity> ExchangeCodeAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException(
            "GitHub OAuth2 exchange must be performed via ASP.NET Core authentication middleware callback.");
    }
}
