using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Sso;

/// <summary>
/// Abstraction for per-organization SAML 2.0 SSO connections.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): SAML 2.0 connection management.
/// Intended implementation: use Sustainsys.Saml2 or ITfoxtec.Identity.Saml2.
/// Supports both SP-initiated and IdP-initiated flows.
/// Just-in-time user provisioning on first SSO login.
/// </remarks>
public interface ISamlConnectionService
{
    /// <summary>Configures a SAML connection for the given organization.</summary>
    Task ConfigureAsync(Guid organizationId, SamlConnectionConfig config, CancellationToken cancellationToken = default);

    /// <summary>Initiates an SP-initiated SAML authentication flow.</summary>
    Task<string> InitiateSpLoginAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

/// <summary>SAML connection configuration.</summary>
/// <remarks>TODO(Phase 2 — implementation): expand with EntityId, ACS URL, certificate, etc.</remarks>
public sealed record SamlConnectionConfig(string MetadataUrl);
