using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Mfa;

/// <summary>
/// TOTP (Time-based One-Time Password) service abstraction.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): TOTP enrollment and verification.
/// Intended library: Otp.NET or System.Security.Cryptography HMACSHA1.
/// Recovery codes: 10 codes, single-use, hashed at rest.
/// </remarks>
public interface ITotpService
{
    /// <summary>Generates a TOTP secret and QR-code URI for enrollment.</summary>
    Task<TotpEnrollmentResult> EnrollAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Verifies a TOTP code against the user's enrolled secret.</summary>
    Task<bool> VerifyAsync(Guid userId, string code, CancellationToken cancellationToken = default);
}

/// <summary>TOTP enrollment result.</summary>
public sealed record TotpEnrollmentResult(string Secret, string QrCodeUri);
