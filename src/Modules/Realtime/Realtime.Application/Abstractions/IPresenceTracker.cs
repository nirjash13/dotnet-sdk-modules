using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Realtime.Application.Abstractions;

/// <summary>
/// Tracks which users are currently connected within each tenant.
/// Default implementation is in-memory; single-instance safe.
/// TODO(Phase 5.7): Redis-backed presence tracker for multi-instance deployments.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Records a user as connected.</summary>
    Task UserConnectedAsync(Guid tenantId, Guid userId, string connectionId, CancellationToken ct = default);

    /// <summary>Records a user as disconnected.</summary>
    Task UserDisconnectedAsync(Guid userId, string connectionId, CancellationToken ct = default);

    /// <summary>Returns the connection IDs for all online users in a tenant.</summary>
    Task<IReadOnlyList<Guid>> GetOnlineUsersAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Returns whether the user has at least one active connection.</summary>
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);
}
