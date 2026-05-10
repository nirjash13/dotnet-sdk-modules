using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Auth;

/// <summary>
/// Magic-link sign-in abstraction (passwordless email auth).
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): magic-link sign-in separate from password reset.
/// Token is single-use, time-boxed (15 min), hashed at rest.
/// </remarks>
public interface IMagicLinkService
{
    /// <summary>Sends a magic-link to the given email address.</summary>
    Task SendAsync(string email, string returnUrl, CancellationToken cancellationToken = default);

    /// <summary>Validates a magic-link token and returns the associated user id, or null if invalid.</summary>
    Task<Guid?> ValidateAsync(string rawToken, CancellationToken cancellationToken = default);
}
