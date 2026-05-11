using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Social;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.SocialLogin;

/// <summary>
/// Microsoft Account OIDC social login adapter.
/// Wires <c>Microsoft.AspNetCore.Authentication.MicrosoftAccount</c>.
/// Configure via <c>Identity:SocialLogin:Microsoft:ClientId</c> and <c>:ClientSecret</c>.
/// </summary>
public sealed class MicrosoftProvider(IConfiguration configuration) : ISocialLoginAdapter
{
    /// <inheritdoc />
    public string ProviderName => "microsoft";

    /// <inheritdoc />
    public Task<string> BuildAuthorizationUrlAsync(string returnUrl, CancellationToken cancellationToken = default)
    {
        string clientId = configuration["Identity:SocialLogin:Microsoft:ClientId"] ?? string.Empty;
        string tenantId = configuration["Identity:SocialLogin:Microsoft:TenantId"] ?? "common";
        string authUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize?" +
                         $"client_id={clientId}&redirect_uri={System.Uri.EscapeDataString(returnUrl)}" +
                         $"&response_type=code&scope=openid+email+profile";
        return Task.FromResult(authUrl);
    }

    /// <inheritdoc />
    public Task<SocialIdentity> ExchangeCodeAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException(
            "Microsoft OIDC exchange must be performed via ASP.NET Core authentication middleware callback.");
    }
}
