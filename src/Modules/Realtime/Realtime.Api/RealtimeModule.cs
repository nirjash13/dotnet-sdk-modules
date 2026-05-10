using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Realtime.Infrastructure.Extensions;
using Realtime.Infrastructure.Hubs;
using SaasBuilder.SharedKernel.Abstractions;

namespace Realtime.Api;

/// <summary><see cref="IModuleStartup"/> for the Realtime module.</summary>
public sealed class RealtimeModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddRealtimeInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        // Map the SignalR hub. No REST endpoints — the hub IS the API.
        // Clients connect via WebSocket or SSE to /hubs/realtime.
        endpoints.MapHub<RealtimeHub>("/hubs/realtime");
    }
}
