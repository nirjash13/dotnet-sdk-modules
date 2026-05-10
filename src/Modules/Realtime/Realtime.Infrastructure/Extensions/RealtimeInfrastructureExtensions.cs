using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Realtime.Application.Abstractions;

namespace Realtime.Infrastructure.Extensions;

/// <summary>Extension methods for registering Realtime module infrastructure services.</summary>
public static class RealtimeInfrastructureExtensions
{
    /// <summary>Registers SignalR and the Realtime module services.</summary>
    public static IServiceCollection AddRealtimeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // SignalR is part of the ASP.NET Core shared framework on .NET 10 — no NuGet package needed.
        services.AddSignalR();

        services.AddScoped<IRealtimeBroadcaster, SignalRRealtimeBroadcaster>();
        services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();

        return services;
    }
}
