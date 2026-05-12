using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Application.Jobs;
using Billing.Domain.Entities;
using Billing.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy.Lifecycle;
using Xunit;

namespace SaasBuilder.IntegrationTests.Phase4;

/// <summary>
/// Load-bearing test: SuspendTenantForUnpaidInvoiceJob suspends a PastDue tenant
/// and skips suspension when the invoice was paid before the grace period elapsed.
/// </summary>
public class DunningJobTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionIsPastDue_SuspendsAndInvokesLifecycleHandler()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        string invoiceId = "in_test123";

        // Build a Subscription in PastDue state via the public domain method.
        Subscription subscription = CreatePastDueSubscription(tenantId, invoiceId);

        Mock<ISubscriptionRepository> repoMock = new Mock<ISubscriptionRepository>();
        repoMock
            .Setup(r => r.FindByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        repoMock
            .Setup(r => r.FindByIdAsync(subscription.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription); // same instance — simulates tracked re-fetch

        bool handlerInvoked = false;
        Mock<ITenantLifecycleHandler> handlerMock = new Mock<ITenantLifecycleHandler>();
        handlerMock
            .Setup(h => h.OnSuspendAsync(tenantId, It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((_, _) => handlerInvoked = true)
            .Returns(Task.CompletedTask);

        SuspendTenantForUnpaidInvoiceJob job = new SuspendTenantForUnpaidInvoiceJob(
            repoMock.Object,
            new[] { handlerMock.Object });

        // Act
        Result result = await job.ExecuteAsync(tenantId, invoiceId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Suspended);
        handlerInvoked.Should().BeTrue("lifecycle handler must be notified on suspension");
        repoMock.Verify(r => r.Update(subscription), Times.Once);
        repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionIsAlreadyPaid_SkipsSuspension()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Subscription activeSubscription = CreateActiveSubscription(tenantId);

        Mock<ISubscriptionRepository> repoMock = new Mock<ISubscriptionRepository>();
        repoMock
            .Setup(r => r.FindByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSubscription);

        SuspendTenantForUnpaidInvoiceJob job = new SuspendTenantForUnpaidInvoiceJob(
            repoMock.Object,
            Array.Empty<ITenantLifecycleHandler>());

        // Act
        Result result = await job.ExecuteAsync(tenantId, "in_old_invoice", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        activeSubscription.Status.Should().Be(SubscriptionStatus.Active, "paid tenant must not be suspended");
        repoMock.Verify(r => r.Update(It.IsAny<Subscription>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoSubscriptionFound_ReturnsFailure()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();

        Mock<ISubscriptionRepository> repoMock = new Mock<ISubscriptionRepository>();
        repoMock
            .Setup(r => r.FindByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        SuspendTenantForUnpaidInvoiceJob job = new SuspendTenantForUnpaidInvoiceJob(
            repoMock.Object,
            Array.Empty<ITenantLifecycleHandler>());

        // Act
        Result result = await job.ExecuteAsync(tenantId, "in_missing", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(tenantId.ToString());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static Subscription CreatePastDueSubscription(Guid tenantId, string invoiceId)
    {
        Subscription sub = Subscription.Create(tenantId, Guid.NewGuid());
        sub.Activate();
        sub.RecordTerminalPaymentFailure(invoiceId);
        return sub;
    }

    private static Subscription CreateActiveSubscription(Guid tenantId)
    {
        Subscription sub = Subscription.Create(tenantId, Guid.NewGuid());
        sub.Activate();
        return sub;
    }
}
