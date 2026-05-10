using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Scim;

/// <summary>
/// SCIM 2.0 group management abstraction (per-org).
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): SCIM 2.0 /scim/v2/Groups endpoint.
/// </remarks>
public interface IScimGroupService
{
    /// <summary>Provisions or updates a group from a SCIM Group resource.</summary>
    Task UpsertAsync(Guid organizationId, ScimGroup group, CancellationToken cancellationToken = default);

    /// <summary>Removes a group.</summary>
    Task RemoveAsync(Guid organizationId, string scimExternalId, CancellationToken cancellationToken = default);
}

/// <summary>Minimal SCIM Group representation.</summary>
/// <remarks>TODO(Phase 2 — implementation): expand per RFC 7643 §4.2.</remarks>
public sealed record ScimGroup(string ExternalId, string DisplayName);
