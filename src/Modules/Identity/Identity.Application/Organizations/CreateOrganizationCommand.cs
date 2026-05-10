using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command to create a new <see cref="Identity.Domain.Organizations.Organization"/> within the current tenant.
/// The calling user becomes the first Owner member.
/// </summary>
/// <param name="TenantId">The tenant owning the organization.</param>
/// <param name="Slug">URL-safe unique slug for the organization.</param>
/// <param name="Name">Human-readable organization name.</param>
/// <param name="OwnerUserId">The user who will be assigned the Owner role at creation.</param>
/// <param name="OwnerRoleId">The id of the system Owner role.</param>
public sealed record CreateOrganizationCommand(
    Guid TenantId,
    string Slug,
    string Name,
    Guid OwnerUserId,
    Guid OwnerRoleId);

/// <summary>
/// Result returned from <see cref="CreateOrganizationHandler"/> on success.
/// </summary>
/// <param name="OrganizationId">The newly-created organization's id.</param>
public sealed record CreateOrganizationResult(Guid OrganizationId);
