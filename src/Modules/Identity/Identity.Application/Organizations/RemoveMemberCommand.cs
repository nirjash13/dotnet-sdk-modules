using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command to remove a member from an organization.
/// Enforces the last-owner-protection invariant.
/// </summary>
/// <param name="OrganizationId">The organization containing the member.</param>
/// <param name="MemberId">The member to remove.</param>
/// <param name="OwnerRoleId">The id of the Owner role (used for last-owner check).</param>
/// <param name="RequestingUserId">The user performing the removal.</param>
public sealed record RemoveMemberCommand(
    Guid OrganizationId,
    Guid MemberId,
    Guid OwnerRoleId,
    Guid RequestingUserId);
