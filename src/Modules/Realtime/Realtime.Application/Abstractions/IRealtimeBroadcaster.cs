using System;
using System.Threading;
using System.Threading.Tasks;

namespace Realtime.Application.Abstractions;

/// <summary>
/// Broadcasts real-time messages to connected clients via SignalR (default)
/// or any other transport registered in DI.
/// </summary>
public interface IRealtimeBroadcaster
{
    /// <summary>Broadcasts to all users in a tenant's group.</summary>
    Task BroadcastToTenantAsync(Guid tenantId, string method, object payload, CancellationToken ct = default);

    /// <summary>Broadcasts to a single user's connection group.</summary>
    Task BroadcastToUserAsync(Guid userId, string method, object payload, CancellationToken ct = default);

    /// <summary>Broadcasts to a named group.</summary>
    Task BroadcastToGroupAsync(string groupName, string method, object payload, CancellationToken ct = default);
}
