using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="ChangeMemberRoleCommand"/>.
/// Computes the active-owner count before delegating to the domain model.
/// </summary>
public sealed class ChangeMemberRoleHandler
{
    private readonly IMemberRepository _members;

    /// <summary>Initializes a new instance of <see cref="ChangeMemberRoleHandler"/>.</summary>
    public ChangeMemberRoleHandler(IMemberRepository members)
    {
        _members = members ?? throw new ArgumentNullException(nameof(members));
    }

    /// <summary>Executes the command. Returns failure if member not found or last-owner protection triggered.</summary>
    public async Task<Result> HandleAsync(
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Member? member = await _members
            .FindByIdAsync(command.MemberId, cancellationToken)
            .ConfigureAwait(false);

        if (member is null || member.OrganizationId != command.OrganizationId)
        {
            return Result.Failure($"Member '{command.MemberId}' not found in organization '{command.OrganizationId}'.");
        }

        IReadOnlyList<Member> activeMembers = await _members
            .GetActiveByOrganizationAsync(command.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        int activeOwnerCount = activeMembers.Count(m => m.RoleId == command.OwnerRoleId && m.Status == MemberStatus.Active);
        bool isCurrentRoleOwner = member.RoleId == command.OwnerRoleId;

        try
        {
            member.ChangeRole(command.NewRoleId, activeOwnerCount, isCurrentRoleOwner);
        }
        catch (LastOwnerProtectionException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _members.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
