using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Realtime.Application.Abstractions;
using StackExchange.Redis;

namespace Realtime.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Realtime module infrastructure services.
/// Backplane is selected by <c>Realtime:Backplane:Provider</c>:
/// <c>Redis | Sql | None</c> (default None = single-instance in-process).
/// When <c>Redis</c> is selected, <c>Realtime:Backplane:Connection</c> must supply
/// the StackExchange.Redis connection string.
/// </summary>
public static class RealtimeInfrastructureExtensions
{
    /// <summary>Registers SignalR and the Realtime module services.</summary>
    public static IServiceCollection AddRealtimeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string backplaneProvider = configuration["Realtime:Backplane:Provider"] ?? string.Empty;
        string? redisConnection = configuration["Realtime:Backplane:Connection"];

        ISignalRServerBuilder signalR = (ISignalRServerBuilder)services.AddSignalR();

        switch (backplaneProvider.Trim().ToUpperInvariant())
        {
            case "REDIS":
                if (!string.IsNullOrWhiteSpace(redisConnection))
                {
                    signalR.AddStackExchangeRedis(redisConnection);

                    // Register Redis multiplexer for RedisPresenceTracker.
                    services.AddSingleton<IConnectionMultiplexer>(
                        _ => ConnectionMultiplexer.Connect(redisConnection));

                    services.AddSingleton<IPresenceTracker, RedisPresenceTracker>();
                }
                else
                {
                    // Config key present but no connection string — warn and fall back.
                    services.AddSingleton<IPresenceTracker>(sp =>
                    {
                        sp.GetRequiredService<ILogger<InMemoryPresenceTracker>>().LogWarning(
                            "Realtime module: Redis backplane requested but 'Realtime:Backplane:Connection' is not set. " +
                            "Falling back to in-memory presence tracker (single-instance only).");
                        return new InMemoryPresenceTracker();
                    });
                }

                break;

            case "SQL":
                // SQL backplane requires Microsoft.AspNetCore.SignalR.SqlServer NuGet package.
                // Warn and fall through to in-memory for Phase 5.7.
                services.AddSingleton<IPresenceTracker>(sp =>
                {
                    sp.GetRequiredService<ILogger<InMemoryPresenceTracker>>().LogWarning(
                        "Realtime module: SQL backplane is not yet fully implemented. " +
                        "Falling back to in-memory presence tracker.");
                    return new InMemoryPresenceTracker();
                });
                break;

            default:
                services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();
                break;
        }

        services.AddScoped<IRealtimeBroadcaster, SignalRRealtimeBroadcaster>();

        return services;
    }
}
