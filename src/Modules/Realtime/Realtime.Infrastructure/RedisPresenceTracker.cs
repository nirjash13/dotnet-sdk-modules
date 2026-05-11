using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Realtime.Application.Abstractions;
using StackExchange.Redis;

namespace Realtime.Infrastructure;

/// <summary>
/// Redis-backed presence tracker. Suitable for multi-instance deployments.
/// Uses Redis sets: <c>presence:tenant:{tenantId}</c> stores userId members
/// and <c>presence:user:{userId}</c> stores connectionId members.
/// TTL-based expiry is not used — connections are removed on disconnect.
/// </summary>
internal sealed class RedisPresenceTracker(IConnectionMultiplexer redis) : IPresenceTracker
{
    private readonly IDatabase _db = redis.GetDatabase();

    /// <inheritdoc />
    public async Task UserConnectedAsync(
        Guid tenantId,
        Guid userId,
        string connectionId,
        CancellationToken ct = default)
    {
        ITransaction tx = _db.CreateTransaction();

        // Add userId to the tenant's user set.
        _ = tx.SetAddAsync(TenantKey(tenantId), userId.ToString());

        // Add connectionId to the user's connection set.
        _ = tx.SetAddAsync(UserKey(userId), connectionId);

        await tx.ExecuteAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UserDisconnectedAsync(
        Guid userId,
        string connectionId,
        CancellationToken ct = default)
    {
        // Remove this specific connection.
        await _db.SetRemoveAsync(UserKey(userId), connectionId).ConfigureAwait(false);

        // If no more connections, also remove from any tenant set.
        long remaining = await _db.SetLengthAsync(UserKey(userId)).ConfigureAwait(false);
        if (remaining == 0)
        {
            // We don't know the tenantId here — scan all tenant keys for this userId.
            // In production, store a reverse mapping (connectionId -> tenantId) in Redis.
            // For Phase 5.7 we keep it simple: the next IsOnlineAsync call will return false.
            await _db.KeyDeleteAsync(UserKey(userId)).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetOnlineUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        RedisValue[] members = await _db.SetMembersAsync(TenantKey(tenantId)).ConfigureAwait(false);

        List<Guid> online = new List<Guid>(members.Length);
        foreach (RedisValue m in members)
        {
            if (Guid.TryParse(m.ToString(), out Guid userId))
            {
                long connections = await _db.SetLengthAsync(UserKey(userId)).ConfigureAwait(false);
                if (connections > 0)
                {
                    online.Add(userId);
                }
            }
        }

        return online;
    }

    /// <inheritdoc />
    public async Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        long connections = await _db.SetLengthAsync(UserKey(userId)).ConfigureAwait(false);
        return connections > 0;
    }

    private static RedisKey TenantKey(Guid tenantId) => $"presence:tenant:{tenantId}";

    private static RedisKey UserKey(Guid userId) => $"presence:user:{userId}";
}
