using System;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Application.Auth;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing tests for <see cref="IAccountLockoutService"/> behavior.
///
/// Load-bearing rationale:
/// 1. Records failed attempt and locks after threshold — proves the lock-out business rule.
///    If the threshold check is removed, the account never locks: user-visible pain.
/// 2. Admin unlock resets counter — proves the unlock flow works end-to-end.
///    If SaveChanges is never called, the unlock silently fails.
/// </summary>
public sealed class AccountLockoutTests
{
    private static readonly Guid UserId = new Guid("aaaaaaaa-0000-0000-0000-000000000001");

    // ── Test 1: Lockout after threshold ───────────────────────────────────────

    /// <summary>
    /// After MaxFailedAttempts failures, <see cref="IAccountLockoutService.RecordFailedAttemptAsync"/>
    /// must lock the account.
    /// </summary>
    [Fact]
    public async Task RecordFailedAttempt_WhenThresholdReached_LocksAccount()
    {
        // Arrange
        var user = User.Create(UserId, "test@example.com", "Test User");
        var lockoutOptions = Options.Create(new LockoutOptions
        {
            MaxFailedAttempts = 3,
            LockoutDurationMinutes = 30,
        });

        var repoMock = new Mock<IUserRepository>();
        // RecordFailedAttemptAsync uses FindByIdForUpdateAsync (tracked entity required for EF mutation).
        repoMock.Setup(r => r.FindByIdForUpdateAsync(UserId, default)).ReturnsAsync(user);
        repoMock.Setup(r => r.SaveChangesAsync(default)).ReturnsAsync(1);

        var loggerMock = new Mock<ILogger<AccountLockoutService>>();

        var service = new AccountLockoutService(repoMock.Object, lockoutOptions, loggerMock.Object);

        // Act — record 3 failures (at the threshold).
        await service.RecordFailedAttemptAsync(UserId, default);
        await service.RecordFailedAttemptAsync(UserId, default);
        await service.RecordFailedAttemptAsync(UserId, default);

        // Assert — account must be locked.
        user.IsLockedOut.Should().BeTrue("account should be locked after MaxFailedAttempts failures");
        user.LockoutUntil.Should().NotBeNull();
        user.LockoutUntil!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    // ── Test 2: Admin unlock resets state ─────────────────────────────────────

    /// <summary>
    /// <see cref="IAccountLockoutService.AdminUnlockAsync"/> must clear lockout and reset the counter.
    /// </summary>
    [Fact]
    public async Task AdminUnlock_WhenAccountIsLocked_ClearsLockoutAndCounter()
    {
        // Arrange — start with a locked user.
        var user = User.Create(UserId, "locked@example.com", "Locked User");
        user.LockOut(DateTimeOffset.UtcNow.AddMinutes(30));

        user.IsLockedOut.Should().BeTrue("precondition: user must be locked");

        var lockoutOptions = Options.Create(new LockoutOptions());
        var repoMock = new Mock<IUserRepository>();
        // AdminUnlockAsync uses FindByIdForUpdateAsync (tracked entity required for EF mutation).
        repoMock.Setup(r => r.FindByIdForUpdateAsync(UserId, default)).ReturnsAsync(user);
        repoMock.Setup(r => r.SaveChangesAsync(default)).ReturnsAsync(1);

        var loggerMock = new Mock<ILogger<AccountLockoutService>>();
        var service = new AccountLockoutService(repoMock.Object, lockoutOptions, loggerMock.Object);

        // Act.
        await service.AdminUnlockAsync(UserId, default);

        // Assert.
        user.IsLockedOut.Should().BeFalse("account should be unlocked after admin unlock");
        user.LockoutUntil.Should().BeNull();
        user.FailedLoginAttempts.Should().Be(0, "failed attempt counter should be reset on admin unlock");

        repoMock.Verify(
            r => r.SaveChangesAsync(default),
            Times.Once,
            "changes must be persisted for the unlock to take effect");
    }
}
