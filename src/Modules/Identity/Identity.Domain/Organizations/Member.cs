using System;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Organizations;

/// <summary>
/// Represents a user's membership within an <see cref="Organization"/>.
/// </summary>
/// <remarks>
/// Invariants:
/// <list type="bullet">
///   <item>An organization must always have at least one active Owner.</item>
///   <item>Removing or demoting the last Owner throws <see cref="LastOwnerProtectionException"/>.</item>
///   <item>Role changes must not leave the organization Owner-less.</item>
/// </list>
/// The Owner-count check is delegated to callers (handlers) that have access to the
/// full member roster. The <see cref="Remove"/> and <see cref="ChangeRole"/> methods
/// accept an <c>activeOwnerCount</c> parameter representing the count before the operation.
/// </remarks>
public sealed class Member
{
    // Private constructor — use factory methods.
    private Member()
    {
    }

    /// <summary>Gets the member's unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the organization this membership belongs to.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Gets the user id.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the role assigned to this member within the organization.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>Gets the current membership status.</summary>
    public MemberStatus Status { get; private set; }

    /// <summary>Gets the UTC timestamp when this member joined the organization.</summary>
    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>
    /// Gets the user id of the person who sent the invitation, if applicable.
    /// <see langword="null"/> when the member was added programmatically (e.g., owner at org creation).
    /// </summary>
    public Guid? InvitedById { get; private set; }

    /// <summary>
    /// Creates an invitation-based <see cref="Member"/> in <see cref="MemberStatus.Invited"/> status.
    /// </summary>
    /// <param name="id">Unique member id.</param>
    /// <param name="organizationId">The organization being joined.</param>
    /// <param name="userId">The invited user.</param>
    /// <param name="roleId">The role the user will assume when the invitation is accepted.</param>
    /// <param name="invitedById">The user who sent the invitation.</param>
    public static Member Invite(Guid id, Guid organizationId, Guid userId, Guid roleId, Guid invitedById)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Member id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("OrganizationId must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("UserId must not be empty.");
        }

        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId must not be empty.");
        }

        return new Member
        {
            Id = id,
            OrganizationId = organizationId,
            UserId = userId,
            RoleId = roleId,
            Status = MemberStatus.Invited,
            JoinedAt = DateTimeOffset.UtcNow,
            InvitedById = invitedById,
        };
    }

    /// <summary>
    /// Creates a directly-added (non-invited) <see cref="Member"/> in <see cref="MemberStatus.Active"/> status.
    /// Used when the organization owner is set at organization creation time.
    /// </summary>
    public static Member CreateActive(Guid id, Guid organizationId, Guid userId, Guid roleId)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Member id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("OrganizationId must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("UserId must not be empty.");
        }

        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId must not be empty.");
        }

        return new Member
        {
            Id = id,
            OrganizationId = organizationId,
            UserId = userId,
            RoleId = roleId,
            Status = MemberStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            InvitedById = null,
        };
    }

    /// <summary>
    /// Transitions this member to <see cref="MemberStatus.Active"/>.
    /// Called when an invited member accepts the invitation.
    /// </summary>
    public void Activate()
    {
        if (Status != MemberStatus.Invited)
        {
            throw new IdentityDomainException($"Cannot activate a member in status {Status}.");
        }

        Status = MemberStatus.Active;
    }

    /// <summary>
    /// Changes the role assigned to this member.
    /// </summary>
    /// <param name="newRoleId">The new role to assign.</param>
    /// <param name="currentActiveOwnerCount">
    /// The number of active Owner-role members in the organization before this change.
    /// Must be provided by the caller who has access to the full member roster.
    /// </param>
    /// <param name="isCurrentRoleOwner">
    /// Whether this member's current role is the Owner role.
    /// The caller must determine this based on the role registry.
    /// </param>
    public void ChangeRole(Guid newRoleId, int currentActiveOwnerCount, bool isCurrentRoleOwner)
    {
        if (newRoleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId must not be empty.");
        }

        // If we're demoting the last owner, block the operation.
        if (isCurrentRoleOwner && currentActiveOwnerCount <= 1)
        {
            throw new LastOwnerProtectionException();
        }

        RoleId = newRoleId;
    }

    /// <summary>
    /// Marks this member as <see cref="MemberStatus.Removed"/>.
    /// </summary>
    /// <param name="currentActiveOwnerCount">
    /// The number of active Owner-role members before this removal.
    /// </param>
    /// <param name="isCurrentRoleOwner">
    /// Whether this member currently holds the Owner role.
    /// </param>
    public void Remove(int currentActiveOwnerCount, bool isCurrentRoleOwner)
    {
        if (Status == MemberStatus.Removed)
        {
            return;
        }

        if (isCurrentRoleOwner && currentActiveOwnerCount <= 1)
        {
            throw new LastOwnerProtectionException();
        }

        Status = MemberStatus.Removed;
    }

    /// <summary>Transitions this member to <see cref="MemberStatus.Suspended"/>.</summary>
    public void Suspend()
    {
        if (Status == MemberStatus.Removed)
        {
            throw new IdentityDomainException("Cannot suspend a removed member.");
        }

        Status = MemberStatus.Suspended;
    }
}
