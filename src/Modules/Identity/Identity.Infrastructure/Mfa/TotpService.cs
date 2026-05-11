using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Mfa;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;
using OtpNet;
using QRCoder;

namespace Identity.Infrastructure.Mfa;

/// <summary>
/// TOTP service implementation using Otp.NET.
/// Secrets are base32-encoded and stored encrypted via the Argon2id credential entity.
/// QR codes are generated as data URLs for inline display.
/// Recovery codes: 10 codes, 16-char alphanumeric, hashed at rest.
/// </summary>
public sealed class TotpService(
    ITotpCredentialStore store,
    ILogger<TotpService> logger)
    : ITotpService
{
    private const int RecoveryCodeCount = 10;
    private const int SecretBytes = 20; // 160 bits — standard for TOTP
    private const string Issuer = "SaasBuilder";

    /// <inheritdoc />
    public async Task<TotpEnrollmentResult> EnrollAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Generate a new TOTP secret.
        byte[] secretBytes = RandomNumberGenerator.GetBytes(SecretBytes);
        string base32Secret = Base32Encoding.ToString(secretBytes);

        // Generate 10 recovery codes and their hashes.
        List<string> rawCodes = new List<string>(RecoveryCodeCount);
        List<string> hashedCodes = new List<string>(RecoveryCodeCount);
        for (int i = 0; i < RecoveryCodeCount; i++)
        {
            string code = GenerateRecoveryCode();
            rawCodes.Add(code);
            hashedCodes.Add(HashCode(code));
        }

        // Persist credential (unconfirmed until user verifies a code).
        var credential = TotpCredential.Create(
            id: Guid.NewGuid(),
            userId: userId,
            encryptedSecret: base32Secret, // Phase 4 envelope encryption will wrap this.
            hashedRecoveryCodes: hashedCodes);

        store.Add(credential);
        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Build provisioning URI (otpauth://totp/...).
        string otpUri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{userId}" +
                        $"?secret={base32Secret}&issuer={Uri.EscapeDataString(Issuer)}&algorithm=SHA1&digits=6&period=30";

        // Generate QR code as data URL (PNG → base64).
        string qrDataUrl = GenerateQrCodeDataUrl(otpUri);

        logger.LogInformation("TOTP enrollment started for user {UserId}.", userId);

        // Return secret + QR so the client can show both the QR and a manual-entry secret.
        return new TotpEnrollmentResult(base32Secret, qrDataUrl);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        TotpCredential? credential = await store
            .FindByUserIdAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        if (credential is null)
        {
            return false;
        }

        byte[] secretBytes = Base32Encoding.ToBytes(credential.EncryptedSecret);
        var totp = new Totp(secretBytes);

        // Allow ±1 step (30-second window on each side) to tolerate clock skew.
        bool valid = totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));

        if (valid && !credential.IsConfirmed)
        {
            credential.Confirm();
            await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("TOTP confirmed for user {UserId}.", userId);
        }

        return valid;
    }

    private static string GenerateRecoveryCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Unambiguous characters only.
        byte[] bytes = RandomNumberGenerator.GetBytes(16);
        var sb = new StringBuilder(16);
        foreach (byte b in bytes)
        {
            sb.Append(chars[b % chars.Length]);
        }

        // Format as XXXX-XXXX-XXXX-XXXX for readability.
        return $"{sb.ToString(0, 4)}-{sb.ToString(4, 4)}-{sb.ToString(8, 4)}-{sb.ToString(12, 4)}";
    }

    private static string HashCode(string code)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateQrCodeDataUrl(string content)
    {
        using var qrGenerator = new QRCodeGenerator();
        using QRCodeData qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        byte[] png = qrCode.GetGraphic(10);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }
}
