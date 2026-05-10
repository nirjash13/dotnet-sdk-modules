using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Impersonation;

/// <summary>
/// System-admin-only impersonation service. Provides time-boxed sessions with mandatory audit.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): safe impersonation per Pigment pattern.
/// - System-admin-only endpoint.
/// - Mandatory reason field; optional approval gate.
/// - Time-boxed session (max 1h, configurable).
/// - Banner header: X-Impersonation: true.
/// - Full audit trail: actor, target, reason, start/end, every action.
/// </remarks>
public interface IImpersonationService
{
    /// <summary>Starts an impersonation session. Returns a short-lived impersonation token.</summary>
    Task<ImpersonationSession> StartAsync(
        Guid adminUserId,
        Guid targetUserId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Ends the current impersonation session.</summary>
    Task EndAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>Active impersonation session.</summary>
public sealed record ImpersonationSession(Guid SessionId, string ImpersonationToken, DateTimeOffset ExpiresAt);
