using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Mfa;

/// <summary>
/// WebAuthn / Passkeys service abstraction.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): WebAuthn registration and authentication flows.
/// Intended library: Fido2NetLib.
/// </remarks>
public interface IWebAuthnService
{
    /// <summary>Begins a WebAuthn registration challenge for the given user.</summary>
    Task<string> BeginRegistrationAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Completes registration with the authenticator response.</summary>
    Task CompleteRegistrationAsync(Guid userId, string attestationResponse, CancellationToken cancellationToken = default);

    /// <summary>Begins a WebAuthn authentication challenge.</summary>
    Task<string> BeginAuthenticationAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Verifies the authenticator assertion response.</summary>
    Task<bool> VerifyAssertionAsync(Guid userId, string assertionResponse, CancellationToken cancellationToken = default);
}
