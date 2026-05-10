using System;
using FluentAssertions;
using Identity.Domain.Organizations;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing domain-level tests for the last-owner-protection invariant.
///
/// Load-bearing rationale:
/// Removing the owner-count guard from <see cref="Member.Remove"/> or <see cref="Member.ChangeRole"/>
/// would suppress the <see cref="LastOwnerProtectionException"/>, silently leaving the organization
/// owner-less. These tests catch that regression at the domain layer without requiring HTTP infrastructure.
///
/// Placed in IntegrationTests to co-locate with other Phase 2 tests.
/// Domain types have no infrastructure dependencies, so no fixture is needed.
/// </summary>
public sealed class LastOwnerProtectionTests
{
    private static readonly Guid OrgId = new Guid("11111111-0000-0000-0000-000000000001");
    private static readonly Guid UserId = new Guid("22222222-0000-0000-0000-000000000002");
    private static readonly Guid OwnerRoleId = new Guid("33333333-0000-0000-0000-000000000003");
    private static readonly Guid MemberRoleId = new Guid("44444444-0000-0000-0000-000000000004");

    // ── Test 5: Remove last owner blocked ──────────────────────────────────────

    /// <summary>
    /// Attempting to remove the sole Owner member throws <see cref="LastOwnerProtectionException"/>.
    /// </summary>
    [Fact]
    public void Member_RemoveLastOwner_ThrowsLastOwnerProtectionException()
    {
        // Arrange — the only active member holds the Owner role.
        Member owner = Member.CreateActive(
            id: Guid.NewGuid(),
            organizationId: OrgId,
            userId: UserId,
            roleId: OwnerRoleId);

        // Act — attempt to remove the last owner (activeOwnerCount = 1, isOwner = true).
        Action act = () => owner.Remove(currentActiveOwnerCount: 1, isCurrentRoleOwner: true);

        // Assert
        act.Should().Throw<LastOwnerProtectionException>(
            because: "removing the last owner must be blocked by the domain invariant");
    }

    // ── Test 6: Change last owner role blocked ─────────────────────────────────

    /// <summary>
    /// Attempting to demote the sole Owner to a non-Owner role throws <see cref="LastOwnerProtectionException"/>.
    /// </summary>
    [Fact]
    public void Member_DemoteLastOwner_ThrowsLastOwnerProtectionException()
    {
        // Arrange — sole active Owner.
        Member owner = Member.CreateActive(
            id: Guid.NewGuid(),
            organizationId: OrgId,
            userId: UserId,
            roleId: OwnerRoleId);

        // Act — demote to Member role (activeOwnerCount = 1, isOwner = true).
        Action act = () => owner.ChangeRole(MemberRoleId, currentActiveOwnerCount: 1, isCurrentRoleOwner: true);

        // Assert
        act.Should().Throw<LastOwnerProtectionException>(
            because: "demoting the last owner must be blocked by the domain invariant");
    }

    // Intentionally not a test: removing a non-last owner succeeds — that path is tested
    // implicitly by the integration flow when a second owner exists.
}
