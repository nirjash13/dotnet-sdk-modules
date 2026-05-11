using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Handlers;
using Entitlements.Application.Abstractions;
using Entitlements.Domain;
using FluentAssertions;
using Moq;
using SaasBuilder.SharedKernel.Abstractions;
using Xunit;

namespace SaasBuilder.IntegrationTests.Admin;

/// <summary>
/// Verifies that <see cref="OverrideEntitlementHandler"/> correctly writes
/// tenant-level entitlement overrides via <see cref="IEntitlementRepository"/>.
/// </summary>
public sealed class EntitlementOverrideHandlerTests
{
    [Fact]
    public async Task Handle_WhenBooleanValue_PersistsOverrideViaRepository()
    {
        Guid tenantId = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
        string key = "feature.advanced-reports";

        EntitlementGrant? capturedGrant = null;
        var repoMock = new Mock<IEntitlementRepository>();
        repoMock.Setup(r => r.AddOverrideAsync(It.IsAny<EntitlementGrant>(), It.IsAny<CancellationToken>()))
            .Callback<EntitlementGrant, CancellationToken>((g, _) => capturedGrant = g)
            .Returns(Task.CompletedTask);

        OverrideEntitlementHandler handler = new OverrideEntitlementHandler(repoMock.Object);

        Result result = await handler.HandleAsync(tenantId, key, "true", "Sales exception", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedGrant.Should().NotBeNull();
        capturedGrant!.BoolValue.Should().BeTrue();

        repoMock.Verify(r => r.AddOverrideAsync(It.IsAny<EntitlementGrant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenKeyIsEmpty_ReturnsValidationFailure()
    {
        var repoMock = new Mock<IEntitlementRepository>();
        OverrideEntitlementHandler handler = new OverrideEntitlementHandler(repoMock.Object);

        Result result = await handler.HandleAsync(Guid.NewGuid(), string.Empty, "true", "reason", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("key");
        repoMock.Verify(r => r.AddOverrideAsync(It.IsAny<EntitlementGrant>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
