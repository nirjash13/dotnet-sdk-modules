using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Social;

/// <summary>
/// Abstraction for OAuth2-based social login providers.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): OIDC adapters for Google, Microsoft, GitHub, Apple.
/// Includes account-linking flow for existing users adding a social provider.
/// </remarks>
public interface ISocialLoginAdapter
{
    /// <summary>Gets the provider name (e.g., "google", "microsoft", "github", "apple").</summary>
    string ProviderName { get; }

    /// <summary>Builds the OAuth2 authorization URL to redirect the user to.</summary>
    Task<string> BuildAuthorizationUrlAsync(string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>Exchanges the authorization code for a user identity.</summary>
    Task<SocialIdentity> ExchangeCodeAsync(string code, string state, CancellationToken cancellationToken = default);
}

/// <summary>Normalized identity returned from a social provider.</summary>
public sealed record SocialIdentity(string ProviderName, string ExternalId, string Email, string? DisplayName);
