using System;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Application.Impersonation;
using Identity.Domain.Entities;
using Identity.Infrastructure.Impersonation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing tests for <see cref="IImpersonationService"/> behavior.
///
/// Load-bearing rationale:
/// 1. Start persists the session and returns a non-empty token.
///    If the session is not persisted, the end-session call cannot find it — user-visible failure.
/// 2. End marks the session as ended.
///    If EndedAt is never set, the audit trail shows the session as perpetually active.
/// </summary>
public sealed class ImpersonationTests
{
    private static readonly Guid AdminId = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TargetId = new Guid("bbbbbbbb-0000-0000-0000-000000000002");

    // 32-byte test-only signing key (base64-encoded). Never used in production.
    private const string TestSigningKey = "dGVzdC1pbXBlcnNvbmF0aW9uLXNpZ25pbmcta2V5LTMyYg==";

    private static IConfiguration BuildTestConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["Identity:ImpersonationSigningKey"] = TestSigningKey,
                ["Identity:Issuer"] = "https://test-issuer",
            })
            .Build();

    // ── Test 1: Start creates auditable session ────────────────────────────────

    [Fact]
    public async Task StartImpersonation_WithValidInput_CreatesSessionAndReturnsToken()
    {
        // Arrange
        var storeMock = new Mock<IImpersonationSessionStore>();
        ImpersonationSessionEntity? stored = null;
        storeMock.Setup(s => s.Add(It.IsAny<ImpersonationSessionEntity>()))
            .Callback<ImpersonationSessionEntity>(s => stored = s);
        storeMock.Setup(s => s.SaveChangesAsync(default)).ReturnsAsync(1);

        var config = BuildTestConfig();
        var service = new ImpersonationService(
            storeMock.Object, config, NullLogger<ImpersonationService>.Instance);

        // Act
        ImpersonationSession session = await service.StartAsync(
            AdminId, TargetId, "Support investigation #1234", default);

        // Assert
        session.SessionId.Should().NotBe(Guid.Empty);
        session.ImpersonationToken.Should().NotBeNullOrWhiteSpace(
            "a non-empty JWT must be returned for the impersonation client to use");
        session.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        stored.Should().NotBeNull("session must be persisted for audit trail");
        stored!.AdminUserId.Should().Be(AdminId);
        stored.TargetUserId.Should().Be(TargetId);
        stored.Reason.Should().Be("Support investigation #1234");
    }

    // ── Test 2: End closes the session ────────────────────────────────────────

    [Fact]
    public async Task EndImpersonation_WithActiveSession_SetsEndedAt()
    {
        // Arrange — create a session entity in-memory.
        var session = ImpersonationSessionEntity.Create(
            id: Guid.NewGuid(),
            adminUserId: AdminId,
            targetUserId: TargetId,
            reason: "Test reason",
            impersonationToken: "fake.jwt.token",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        var storeMock = new Mock<IImpersonationSessionStore>();
        storeMock.Setup(s => s.FindByIdAsync(session.Id, default)).ReturnsAsync(session);
        storeMock.Setup(s => s.SaveChangesAsync(default)).ReturnsAsync(1);

        var config = BuildTestConfig();
        var service = new ImpersonationService(
            storeMock.Object, config, NullLogger<ImpersonationService>.Instance);

        // Act
        await service.EndAsync(session.Id, default);

        // Assert
        session.IsActive.Should().BeFalse("session must be inactive after ending");
        session.EndedAt.Should().NotBeNull("EndedAt must be set for audit trail completeness");

        storeMock.Verify(
            s => s.SaveChangesAsync(default),
            Times.Once,
            "changes must be persisted for the end to be durable");
    }
}
