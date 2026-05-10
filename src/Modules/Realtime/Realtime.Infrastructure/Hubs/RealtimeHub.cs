using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Realtime.Application.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Realtime.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time bidirectional communication.
/// Mapped at <c>/hubs/realtime</c>.
/// On connect, the client is automatically added to:
/// - <c>tenant:{tenantId}</c> group for tenant-scoped broadcasts
/// - <c>user:{userId}</c> group for user-targeted pushes
/// Connections without a valid tenant context are immediately rejected.
/// </summary>
[Authorize]
public sealed class RealtimeHub(
    ITenantContextAccessor tenantAccessor,
    IPresenceTracker presenceTracker)
    : Hub
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            // Reject connection — no tenant context.
            Context.Abort();
            return;
        }

        // Join tenant group for broadcast to all tenant users.
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{ctx.TenantId}").ConfigureAwait(false);

        if (ctx.UserId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{ctx.UserId}").ConfigureAwait(false);
            await presenceTracker.UserConnectedAsync(ctx.TenantId, ctx.UserId.Value, Context.ConnectionId)
                .ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx?.UserId.HasValue == true)
        {
            await presenceTracker.UserDisconnectedAsync(ctx.UserId.Value, Context.ConnectionId)
                .ConfigureAwait(false);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}
