using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command: removes an <see cref="Identity.Domain.Organizations.OrganizationDomainClaim"/>.
/// </summary>
/// <param name="OrganizationId">The owning organization.</param>
/// <param name="ClaimId">The claim to delete.</param>
public sealed record DeleteDomainClaimCommand(Guid OrganizationId, Guid ClaimId);
