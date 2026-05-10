using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command to accept a pending invitation using its raw (pre-hash) token.
/// Validates the token, marks the invitation redeemed, and creates an active member.
/// </summary>
/// <param name="RawToken">The plain-text token extracted from the invitation link.</param>
/// <param name="UserId">The user accepting the invitation.</param>
public sealed record AcceptInvitationCommand(string RawToken, Guid UserId);

/// <summary>Result returned on successful acceptance.</summary>
/// <param name="OrganizationId">The organization the user has joined.</param>
/// <param name="MemberId">The newly-created member id.</param>
public sealed record AcceptInvitationResult(Guid OrganizationId, Guid MemberId);
