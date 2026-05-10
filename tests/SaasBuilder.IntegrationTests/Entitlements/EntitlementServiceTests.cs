using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Entitlements.Application;
using Entitlements.Application.Abstractions;
using Entitlements.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SaasBuilder.SharedKernel.Tenancy;
using Xunit;

namespace SaasBuilder.IntegrationTests.Entitlements;

/// <summary>
/// Load-bearing entitlement service tests.
///
/// Test 1 (tenant lacks entitlement → HasAsync returns false):
///   Fails if EntitlementService.HasAsync returns true when the repository has no matching grant.
///
/// Test 2 (tenant override grants entitlement even without edition grant):
///   Fails if the tenant-override-wins logic is removed from EntitlementRepository merge.
/// </summary>
public sealed class EntitlementServiceTests
{
    private static readonly Guid TenantId = new Guid("e1e1e1e1-0001-0000-0000-000000000001");

    private static ITenantContextAccessor BuildAccessor(Guid tenantId)
    {
        Mock<ITenantContext> ctx = new Mock<ITenantContext>();
        ctx.Setup(c => c.TenantId).Returns(tenantId);

        Mock<ITenantContextAccessor> accessor = new Mock<ITenantContextAccessor>();
        accessor.Setup(a => a.Current).Returns(ctx.Object);
        return accessor.Object;
    }

    [Fact]
    public async Task HasAsync_WhenEntitlementNotGranted_ReturnsFalse()
    {
        // Arrange — repository returns no grants.
        Mock<IEntitlementRepository> repo = new Mock<IEntitlementRepository>();
        repo.Setup(r => r.GetEffectiveGrantsAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EntitlementGrant>());

        EntitlementService service = new EntitlementService(
            repo.Object,
            BuildAccessor(TenantId),
            NullLogger<EntitlementService>.Instance);

        // Act
        bool result = await service.HasAsync("advanced_reporting", CancellationToken.None);

        // Assert
        result.Should().BeFalse(because: "a tenant with no grants must not have the entitlement");
    }

    [Fact]
    public async Task HasAsync_WhenTenantOverrideGrants_ReturnsTrue()
    {
        // Arrange — a sales override grants the entitlement even though no edition is configured.
        EntitlementGrant grant = EntitlementGrant.ForTenantOverrideBoolean(TenantId, "advanced_reporting", true);

        Mock<IEntitlementRepository> repo = new Mock<IEntitlementRepository>();
        repo.Setup(r => r.GetEffectiveGrantsAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EntitlementGrant> { grant });

        EntitlementService service = new EntitlementService(
            repo.Object,
            BuildAccessor(TenantId),
            NullLogger<EntitlementService>.Instance);

        // Act
        bool result = await service.HasAsync("advanced_reporting", CancellationToken.None);

        // Assert
        result.Should().BeTrue(because: "a tenant override must grant the entitlement");
    }
}
