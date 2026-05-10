using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command to change the role assigned to an existing organization member.
/// Enforces the last-owner-protection invariant.
/// </summary>
/// <param name="OrganizationId">The organization containing the member.</param>
/// <param name="MemberId">The member whose role is being changed.</param>
/// <param name="NewRoleId">The new role to assign.</param>
/// <param name="OwnerRoleId">The id of the system Owner role (used for last-owner check).</param>
/// <param name="RequestingUserId">The user performing the role change.</param>
public sealed record ChangeMemberRoleCommand(
    Guid OrganizationId,
    Guid MemberId,
    Guid NewRoleId,
    Guid OwnerRoleId,
    Guid RequestingUserId);
