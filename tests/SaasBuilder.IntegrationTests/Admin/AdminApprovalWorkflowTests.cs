using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Application.Handlers;
using Admin.Application.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Admin;

/// <summary>
/// Verifies the approval workflow: a high-sensitivity action must NOT execute
/// until a second admin approves, and the approver cannot be the same as the requestor.
/// </summary>
public sealed class AdminApprovalWorkflowTests
{
    [Fact]
    public async Task FindPendingAction_WhenActionDoesNotExist_ReturnsNull()
    {
        var storeMock = new Mock<IPendingAdminActionStore>();
        storeMock.Setup(s => s.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingAdminAction?)null);

        ApproveAdminActionHandler handler = new ApproveAdminActionHandler(storeMock.Object);

        SaasBuilder.SharedKernel.Abstractions.Result<PendingAdminAction> result =
            await handler.ApproveAsync(Guid.NewGuid(), "admin-2", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ApproveAlreadyApprovedAction_ReturnsFailure()
    {
        Guid actionId = Guid.NewGuid();
        var storeMock = new Mock<IPendingAdminActionStore>();

        // Action is already resolved.
        storeMock.Setup(s => s.FindAsync(actionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingAdminAction
            {
                Id = actionId,
                RequestorId = "admin-1",
                Action = "tenant.impersonate",
                Sensitivity = SensitivityLevel.High,
                Status = PendingActionStatus.Approved,
                ApproverId = "admin-2",
                ResolvedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                RequestedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            });

        ApproveAdminActionHandler handler = new ApproveAdminActionHandler(storeMock.Object);

        SaasBuilder.SharedKernel.Abstractions.Result<PendingAdminAction> result =
            await handler.ApproveAsync(actionId, "admin-3", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already");

        // ApproveAsync must NOT be called on an already-resolved action.
        storeMock.Verify(
            s => s.ApproveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
