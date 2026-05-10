using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Scim;

/// <summary>
/// SCIM 2.0 user management abstraction (per-org).
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): SCIM 2.0 /scim/v2/Users endpoint.
/// Conformance test pack: Okta + Microsoft Entra.
/// Bearer-token authentication per org SCIM endpoint.
/// </remarks>
public interface IScimUserService
{
    /// <summary>Provisions or updates a user from a SCIM User resource.</summary>
    Task UpsertAsync(Guid organizationId, ScimUser user, CancellationToken cancellationToken = default);

    /// <summary>Deprovisions a user (soft-delete / remove from org).</summary>
    Task DeprovisionAsync(Guid organizationId, string scimExternalId, CancellationToken cancellationToken = default);
}

/// <summary>Minimal SCIM User representation.</summary>
/// <remarks>TODO(Phase 2 — implementation): expand per RFC 7643 §4.1.</remarks>
public sealed record ScimUser(string ExternalId, string Email, string DisplayName);
