using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command: verifies the DNS TXT record for a pending domain claim.
/// </summary>
/// <param name="OrganizationId">The owning organization.</param>
/// <param name="ClaimId">The claim to verify.</param>
public sealed record VerifyDomainClaimCommand(Guid OrganizationId, Guid ClaimId);
