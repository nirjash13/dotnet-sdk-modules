using System;
using System.Threading;
using System.Threading.Tasks;
using Ai.Domain.Entities;
using Ai.Infrastructure.Budgets;
using Ai.Infrastructure.VectorStores;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Ai;

/// <summary>
/// Budget tracker tests: spending above the hard cap (100%) must be detectable so callers
/// can return HTTP 402. This test uses an in-memory DB for isolation — no PostgreSQL required.
/// </summary>
public sealed class LlmBudgetTrackerTests
{
    private static AiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AiDbContext(options);
    }

    [Fact]
    public async Task IsBudgetExceededAsync_WhenSpendExceedsBudget_ReturnsTrue()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        decimal budget = 10.00m; // $10 per month

        var entitlements = new Mock<IEntitlementValueProvider>();
        entitlements
            .Setup(e => e.GetValueAsync<decimal>(tenantId, "ai_budget_usd_per_month", It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        await using AiDbContext db = CreateDbContext();

        // Record $11 of spend — exceeds the $10 budget.
        db.LlmUsageRecords.Add(LlmUsageRecord.Create(
            tenantId,
            Guid.NewGuid(),
            "gpt-4o",
            promptTokens: 1000,
            completionTokens: 500,
            costUsd: 11.00m,
            requestId: "req-001"));

        await db.SaveChangesAsync(CancellationToken.None);

        var tracker = new EfCoreLlmBudgetTracker(
            db,
            entitlements.Object,
            NullLogger<EfCoreLlmBudgetTracker>.Instance);

        // Act
        bool exceeded = await tracker.IsBudgetExceededAsync(tenantId, CancellationToken.None);

        // Assert
        exceeded.Should().BeTrue("$11 spent against a $10 budget must be flagged as exceeded");
    }

    [Fact]
    public async Task IsBudgetExceededAsync_WhenSpendIsBelowBudget_ReturnsFalse()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        decimal budget = 10.00m;

        var entitlements = new Mock<IEntitlementValueProvider>();
        entitlements
            .Setup(e => e.GetValueAsync<decimal>(tenantId, "ai_budget_usd_per_month", It.IsAny<CancellationToken>()))
            .ReturnsAsync(budget);

        await using AiDbContext db = CreateDbContext();

        db.LlmUsageRecords.Add(LlmUsageRecord.Create(
            tenantId, Guid.NewGuid(), "gpt-4o-mini", 100, 50, 0.50m, "req-002"));

        await db.SaveChangesAsync(CancellationToken.None);

        var tracker = new EfCoreLlmBudgetTracker(
            db,
            entitlements.Object,
            NullLogger<EfCoreLlmBudgetTracker>.Instance);

        // Act
        bool exceeded = await tracker.IsBudgetExceededAsync(tenantId, CancellationToken.None);

        // Assert
        exceeded.Should().BeFalse("$0.50 against a $10 budget is not exceeded");
    }
}
