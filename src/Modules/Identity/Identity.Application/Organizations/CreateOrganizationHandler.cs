using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="CreateOrganizationCommand"/>.
/// Creates the organization aggregate and seeds the calling user as the first Owner.
/// </summary>
public sealed class CreateOrganizationHandler
{
    private readonly IOrganizationRepository _organizations;
    private readonly IMemberRepository _members;

    /// <summary>Initializes a new instance of <see cref="CreateOrganizationHandler"/>.</summary>
    public CreateOrganizationHandler(
        IOrganizationRepository organizations,
        IMemberRepository members)
    {
        _organizations = organizations ?? throw new ArgumentNullException(nameof(organizations));
        _members = members ?? throw new ArgumentNullException(nameof(members));
    }

    /// <summary>
    /// Executes the command. Returns a failure result if the slug is already taken.
    /// </summary>
    public async Task<Result<CreateOrganizationResult>> HandleAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Organization? existing = await _organizations
            .FindBySlugAsync(command.Slug, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Result<CreateOrganizationResult>.Failure($"Slug '{command.Slug}' is already taken.");
        }

        Organization org = Organization.Create(
            id: Guid.NewGuid(),
            tenantId: command.TenantId,
            slug: command.Slug,
            name: command.Name);

        _organizations.Add(org);

        Member ownerMember = Member.CreateActive(
            id: Guid.NewGuid(),
            organizationId: org.Id,
            userId: command.OwnerUserId,
            roleId: command.OwnerRoleId);

        _members.Add(ownerMember);

        await _organizations.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<CreateOrganizationResult>.Success(new CreateOrganizationResult(org.Id));
    }
}
