// NOTE: Handler unit test — lives here because no dedicated unit-test project exists for this module.
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Application.Auth;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing regression test for C-13: password reset was a silent no-op
/// (the new password was never persisted to the user entity's <see cref="User.PasswordHash"/>).
///
/// Failure signal: if <see cref="PasswordResetService.CompleteAsync"/> no longer calls
/// <see cref="IUserRepository.FindByIdForUpdateAsync"/> + <see cref="User.SetPasswordHash"/>
/// + <see cref="IUserRepository.SaveChangesAsync"/>, this test fails.
///
/// User-visible consequence: users who reset their password can still log in with their
/// old credential, meaning the reset silently does nothing.
/// </summary>
public sealed class PasswordResetServiceTests
{
    private static readonly Guid UserId = new Guid("cccccccc-0000-0000-0000-000000000001");

    [Fact]
    public async Task CompleteAsync_WithValidToken_PersistsNewPasswordHashAndReturnsTrue()
    {
        // Arrange
        // A valid, unused, unexpired token.
        PasswordResetToken token = PasswordResetToken.Create(
            id: new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            userId: UserId,
            tokenHash: ComputeHash("valid-raw-token"),
            expiresAt: DateTimeOffset.UtcNow.AddHours(1));

        // A tracked user (simulates FindByIdForUpdateAsync returning a change-tracked entity).
        User user = BuildUser(UserId);

        var store = new Mock<IPasswordResetTokenStore>();
        store
            .Setup(s => s.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        store
            .Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var repo = new Mock<IUserRepository>();
        repo
            .Setup(r => r.FindByIdForUpdateAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        repo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var hasher = new Mock<IArgon2idHasher>();
        hasher
            .Setup(h => h.Hash("new-password-123"))
            .Returns("argon2id-hash-of-new-password");

        var service = new PasswordResetService(
            store.Object,
            repo.Object,
            hasher.Object,
            NullLogger<PasswordResetService>.Instance);

        // Act
        bool result = await service.CompleteAsync("valid-raw-token", "new-password-123", CancellationToken.None);

        // Assert — service must report success.
        result.Should().BeTrue(because: "the token is valid and the user exists");

        // The new password hash must have been written to the user entity.
        user.PasswordHash.Should().Be(
            "argon2id-hash-of-new-password",
            because: "CompleteAsync must call SetPasswordHash on the tracked user");

        // The token must be marked used (replay protection).
        token.IsValid.Should().BeFalse(because: "a consumed token must not be reusable");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ComputeHash(string raw)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(bytes);
    }

    private static User BuildUser(Guid userId)
        => User.Create(userId, "user@example.com", "Test User");
}
