using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Auth;

/// <summary>
/// Password reset via email magic link.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): password reset flow.
/// - Token expiry + reuse protection.
/// - Account lockout after N failed attempts (admin unlock endpoint).
/// - Argon2id hashing upgrade path:
///   Current OpenIddict store likely uses PBKDF2. Upgrade to Argon2id once
///   IArgon2idHasher (Identity.Application/Auth/IArgon2idHasher.cs) is implemented.
///   Migration: on next login, re-hash the verified password with Argon2id in place.
/// </remarks>
public interface IPasswordResetService
{
    /// <summary>Initiates a password reset by sending a reset email.</summary>
    Task InitiateAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Completes the reset using the raw token and a new password.</summary>
    Task<bool> CompleteAsync(string rawToken, string newPassword, CancellationToken cancellationToken = default);
}
