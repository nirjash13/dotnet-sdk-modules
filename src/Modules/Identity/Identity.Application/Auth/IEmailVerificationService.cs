using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Auth;

/// <summary>
/// Email verification service abstraction.
/// Tokens are single-use, SHA-256 hashed at rest, and expire after 24 hours.
/// </summary>
public interface IEmailVerificationService
{
    /// <summary>
    /// Generates a verification token and (in production) sends a verification email.
    /// In the current implementation the email dispatch is stubbed — wire up the
    /// Notifications module (Phase 5) to send real emails.
    /// </summary>
    /// <param name="userId">The user requiring email verification.</param>
    /// <param name="email">The email address to verify.</param>
    /// <param name="cancellationToken">Propagated cancellation.</param>
    Task SendVerificationEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and consumes the token. Returns <see langword="true"/> and marks the
    /// user's email as verified on success; returns <see langword="false"/> if the token
    /// is invalid, expired, or already used.
    /// </summary>
    Task<bool> VerifyAsync(string rawToken, CancellationToken cancellationToken = default);
}
