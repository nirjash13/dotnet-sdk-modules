using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Sso;

/// <summary>
/// Abstraction for per-organization OIDC SSO connections.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): per-org OIDC connection configurator.
/// Supports dynamic OIDC providers (Okta, Azure AD, etc.) per organization.
/// </remarks>
public interface IOidcConnectionService
{
    /// <summary>Configures an OIDC connection for the given organization.</summary>
    Task ConfigureAsync(Guid organizationId, OidcConnectionConfig config, CancellationToken cancellationToken = default);
}

/// <summary>OIDC connection configuration.</summary>
/// <remarks>TODO(Phase 2 — implementation): expand with ClientId, ClientSecret, Authority, etc.</remarks>
public sealed record OidcConnectionConfig(string Authority, string ClientId);
