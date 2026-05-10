using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Realtime.Application.Abstractions;

namespace Realtime.Infrastructure;

/// <summary>
/// In-memory presence tracker. Thread-safe for single-instance deployments.
/// TODO(Phase 5.7): replace with Redis-backed presence tracker for multi-instance.
/// </summary>
internal sealed class InMemoryPresenceTracker : IPresenceTracker
{
    // userId -> set of connection ids
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, bool>> _connections = new();

    // connectionId -> tenantId (for reverse lookup on disconnect)
    private readonly ConcurrentDictionary<string, (Guid TenantId, Guid UserId)> _connectionMeta = new();

    // tenantId -> set of user ids
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, bool>> _tenantUsers = new();

    /// <inheritdoc />
    public Task UserConnectedAsync(Guid tenantId, Guid userId, string connectionId, CancellationToken ct = default)
    {
        ConcurrentDictionary<string, bool> userConnections =
            _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, bool>());
        userConnections[connectionId] = true;

        _connectionMeta[connectionId] = (tenantId, userId);

        ConcurrentDictionary<Guid, bool> tenantUsers =
            _tenantUsers.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, bool>());
        tenantUsers[userId] = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UserDisconnectedAsync(Guid userId, string connectionId, CancellationToken ct = default)
    {
        if (_connections.TryGetValue(userId, out ConcurrentDictionary<string, bool>? userConnections))
        {
            userConnections.TryRemove(connectionId, out _);

            if (userConnections.IsEmpty && _connectionMeta.TryRemove(connectionId, out (Guid TenantId, Guid) meta))
            {
                if (_tenantUsers.TryGetValue(meta.TenantId, out ConcurrentDictionary<Guid, bool>? tenantUsers))
                {
                    tenantUsers.TryRemove(userId, out _);
                }
            }
        }

        _connectionMeta.TryRemove(connectionId, out _);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> GetOnlineUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (!_tenantUsers.TryGetValue(tenantId, out ConcurrentDictionary<Guid, bool>? users))
        {
            return Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
        }

        IReadOnlyList<Guid> online = users.Keys.ToList();
        return Task.FromResult(online);
    }

    /// <inheritdoc />
    public Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        bool online = _connections.TryGetValue(userId, out ConcurrentDictionary<string, bool>? conns)
            && !conns.IsEmpty;
        return Task.FromResult(online);
    }
}
