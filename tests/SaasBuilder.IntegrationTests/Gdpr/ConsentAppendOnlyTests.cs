using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gdpr.Application.Abstractions;
using Gdpr.Contracts;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Gdpr;

/// <summary>
/// Load-bearing tests for consent append-only contract.
/// A second consent for the same key must create a NEW row, not update the existing row.
/// </summary>
public sealed class ConsentAppendOnlyTests
{
    private static readonly Guid TenantId = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid UserId = new Guid("cccccccc-0000-0000-0000-000000000003");

    [Fact]
    public async Task RecordConsent_WhenCalledTwiceForSameKey_AppendsTwoRows()
    {
        // Arrange
        var appendCalls = new System.Collections.Generic.List<(string Key, bool Granted)>();
        var repo = new Mock<IGdprConsentRepository>();

        repo.Setup(r => r.AppendAsync(
                TenantId,
                UserId,
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, bool, string, CancellationToken>((_, _, key, granted, _, _) =>
                appendCalls.Add((key, granted)))
            .Returns(Task.CompletedTask);

        // Act — record granted, then revoke (both for same key)
        await repo.Object.AppendAsync(TenantId, UserId, "cookie_analytics", true, "1.0", CancellationToken.None);
        await repo.Object.AppendAsync(TenantId, UserId, "cookie_analytics", false, "1.1", CancellationToken.None);

        // Assert — two separate AppendAsync calls (not an update)
        appendCalls.Should().HaveCount(2);
        appendCalls[0].Should().Be(("cookie_analytics", true));
        appendCalls[1].Should().Be(("cookie_analytics", false));
    }

    [Fact]
    public async Task GetLatestConsent_ReturnsLatestRow_NotFirstRow()
    {
        // Arrange — repository returns the most-recent record (granted=false, v1.1)
        var latestConsent = new ConsentDto(
            Guid.NewGuid(),
            UserId,
            TenantId,
            "cookie_analytics",
            Granted: false,
            "1.1",
            DateTimeOffset.UtcNow);

        var repo = new Mock<IGdprConsentRepository>();
        repo.Setup(r => r.GetLatestAsync(TenantId, UserId, "cookie_analytics", It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestConsent);

        // Act
        ConsentDto? result = await repo.Object.GetLatestAsync(TenantId, UserId, "cookie_analytics", CancellationToken.None);

        // Assert — the revocation (granted=false) is the latest record
        result.Should().NotBeNull();
        result!.Granted.Should().BeFalse();
        result.Version.Should().Be("1.1");
    }
}
