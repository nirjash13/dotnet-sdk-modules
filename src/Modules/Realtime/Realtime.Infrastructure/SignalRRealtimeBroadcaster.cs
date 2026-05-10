using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Realtime.Application.Abstractions;
using Realtime.Infrastructure.Hubs;

namespace Realtime.Infrastructure;

/// <summary>
/// <see cref="IRealtimeBroadcaster"/> implementation backed by ASP.NET Core SignalR.
/// Uses in-process hub context — no Redis/SQL backplane required for single-instance deployments.
/// TODO(Phase 5.7): add Redis backplane via Microsoft.AspNetCore.SignalR.StackExchangeRedis
///                  for multi-instance deployments.
/// </summary>
internal sealed class SignalRRealtimeBroadcaster(IHubContext<RealtimeHub> hubContext)
    : IRealtimeBroadcaster
{
    /// <inheritdoc />
    public Task BroadcastToTenantAsync(Guid tenantId, string method, object payload, CancellationToken ct = default)
        => hubContext.Clients.Group($"tenant:{tenantId}").SendAsync(method, payload, ct);

    /// <inheritdoc />
    public Task BroadcastToUserAsync(Guid userId, string method, object payload, CancellationToken ct = default)
        => hubContext.Clients.Group($"user:{userId}").SendAsync(method, payload, ct);

    /// <inheritdoc />
    public Task BroadcastToGroupAsync(string groupName, string method, object payload, CancellationToken ct = default)
        => hubContext.Clients.Group(groupName).SendAsync(method, payload, ct);
}
