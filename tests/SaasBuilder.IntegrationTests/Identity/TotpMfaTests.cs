using System;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Application.Mfa;
using Identity.Domain.Entities;
using Identity.Infrastructure.Mfa;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OtpNet;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing tests for <see cref="ITotpService"/> behavior.
///
/// Load-bearing rationale:
/// 1. Enroll returns a valid base32 secret and a QR code data URL.
///    If the secret format is broken, authenticator apps cannot import it — user-visible failure.
/// 2. Verify with a correct TOTP code returns true.
///    If the TOTP window logic is wrong, every valid MFA attempt is rejected.
/// 3. Verify with an unknown user returns false (not an exception).
///    If this throws instead of returning false, the endpoint produces a 500 instead of 400.
/// </summary>
public sealed class TotpMfaTests
{
    private static readonly Guid UserId = new Guid("aaaaaaaa-0000-0000-0000-000000000001");

    // ── Test 1: Enrollment returns a valid secret and QR URL ─────────────────

    [Fact]
    public async Task EnrollTotp_ReturnsValidBase32SecretAndQrUrl()
    {
        // Arrange
        TotpCredential? stored = null;
        var storeMock = new Mock<ITotpCredentialStore>();
        storeMock.Setup(s => s.Add(It.IsAny<TotpCredential>()))
            .Callback<TotpCredential>(c => stored = c);
        storeMock.Setup(s => s.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = new TotpService(storeMock.Object, NullLogger<TotpService>.Instance);

        // Act
        TotpEnrollmentResult result = await service.EnrollAsync(UserId, default);

        // Assert — secret must be valid base32 (Otp.NET accepts it without throwing).
        result.Secret.Should().NotBeNullOrWhiteSpace();
        byte[] secretBytes = Base32Encoding.ToBytes(result.Secret); // throws if invalid base32
        secretBytes.Should().HaveCountGreaterThan(0, "a valid base32 secret must decode to bytes");

        result.QrCodeUri.Should().StartWith("data:image/png;base64,",
            "QR code must be returned as a data URL for inline display");

        stored.Should().NotBeNull("credential must be persisted");
        stored!.IsConfirmed.Should().BeFalse("credential is unconfirmed until user verifies first code");
    }

    // ── Test 2: Verify with correct current code returns true ─────────────────

    [Fact]
    public async Task VerifyTotp_WithValidCode_ReturnsTrue()
    {
        // Arrange — set up a known secret.
        byte[] secretBytes = Base32Encoding.ToBytes("JBSWY3DPEHPK3PXP");
        string base32Secret = Base32Encoding.ToString(secretBytes);

        var credential = TotpCredential.Create(
            id: Guid.NewGuid(),
            userId: UserId,
            encryptedSecret: base32Secret,
            hashedRecoveryCodes: new[] { "hash1", "hash2" });

        var storeMock = new Mock<ITotpCredentialStore>();
        storeMock.Setup(s => s.FindByUserIdAsync(UserId, default)).ReturnsAsync(credential);
        storeMock.Setup(s => s.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = new TotpService(storeMock.Object, NullLogger<TotpService>.Instance);

        // Generate a valid TOTP code using the same secret.
        var totp = new Totp(secretBytes);
        string currentCode = totp.ComputeTotp();

        // Act
        bool result = await service.VerifyAsync(UserId, currentCode, default);

        // Assert
        result.Should().BeTrue("a correctly-generated TOTP code must pass verification");
    }

    // ── Test 3: Verify for unknown user returns false ─────────────────────────

    [Fact]
    public async Task VerifyTotp_WhenNoCredentialExists_ReturnsFalse()
    {
        // Arrange
        var storeMock = new Mock<ITotpCredentialStore>();
        storeMock.Setup(s => s.FindByUserIdAsync(UserId, default))
            .ReturnsAsync((TotpCredential?)null);

        var service = new TotpService(storeMock.Object, NullLogger<TotpService>.Instance);

        // Act
        bool result = await service.VerifyAsync(UserId, "123456", default);

        // Assert
        result.Should().BeFalse("missing credential must return false, not throw");
    }
}
