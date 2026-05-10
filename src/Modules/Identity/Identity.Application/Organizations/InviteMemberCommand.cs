using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command to invite a user (by email) to an organization.
/// Generates a hashed token and persists the invitation; dispatches <c>MemberInvited</c> integration event.
/// </summary>
/// <param name="OrganizationId">The target organization.</param>
/// <param name="Email">The invitee's email address.</param>
/// <param name="RoleId">The role to assign on acceptance.</param>
/// <param name="InvitedByUserId">The user performing the invitation.</param>
public sealed record InviteMemberCommand(
    Guid OrganizationId,
    string Email,
    Guid RoleId,
    Guid InvitedByUserId);

/// <summary>Result returned on successful invitation creation.</summary>
/// <param name="InvitationId">The newly-created invitation id.</param>
/// <param name="RawToken">The plain-text token to include in the invitation email (not stored).</param>
public sealed record InviteMemberResult(Guid InvitationId, string RawToken);
