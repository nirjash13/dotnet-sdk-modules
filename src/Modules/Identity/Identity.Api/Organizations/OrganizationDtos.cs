using System;

namespace Identity.Api.Organizations;

// ── Request DTOs ──────────────────────────────────────────────────────────────

/// <summary>Request body for POST /api/v1/organizations.</summary>
public sealed record CreateOrganizationRequest(string Slug, string Name);

/// <summary>Request body for PATCH /api/v1/organizations/{id}.</summary>
public sealed record RenameOrganizationRequest(string Name);

/// <summary>Request body for POST /api/v1/organizations/{id}/members:invite.</summary>
public sealed record InviteMemberRequest(string Email, Guid RoleId);

/// <summary>Request body for POST /api/v1/invitations/{token}:accept.</summary>
public sealed record AcceptInvitationRequest(Guid UserId);

/// <summary>Request body for PATCH /api/v1/organizations/{id}/members/{memberId}/role.</summary>
public sealed record ChangeMemberRoleRequest(Guid NewRoleId, Guid OwnerRoleId);

/// <summary>Request body for DELETE /api/v1/organizations/{id}/members/{memberId}.</summary>
public sealed record RemoveMemberRequest(Guid OwnerRoleId);

/// <summary>Request body for POST /api/v1/organizations/{id}:transfer-ownership.</summary>
public sealed record TransferOwnershipRequest(Guid NewOwnerUserId, Guid OwnerRoleId);

// ── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>Response DTO for organization create.</summary>
public sealed record CreateOrganizationResponse(Guid OrganizationId);

/// <summary>Response DTO for organization summary (list / get).</summary>
public sealed record OrganizationSummaryResponse(Guid Id, string Slug, string Name, string Status);

/// <summary>Response DTO for invite member.</summary>
public sealed record InviteMemberResponse(Guid InvitationId);

/// <summary>Response DTO for accept invitation.</summary>
public sealed record AcceptInvitationResponse(Guid OrganizationId, Guid MemberId);
