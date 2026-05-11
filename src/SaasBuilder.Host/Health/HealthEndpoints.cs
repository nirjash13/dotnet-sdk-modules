using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SaasBuilder.Host.Health;

/// <summary>
/// Maps the three standard health probe endpoints required by Kubernetes and the SaasBuilder Helm chart.
/// <list type="bullet">
/// <item><description><c>GET /health/live</c> — liveness: process is alive (always 200, no deps checked).</description></item>
/// <item><description><c>GET /health/ready</c> — readiness: external dependencies (DB, RabbitMQ, Redis) are healthy.</description></item>
/// <item><description><c>GET /health/startup</c> — startup: migrations applied flag is set.</description></item>
/// </list>
/// The legacy <c>GET /health</c> endpoint is kept as an alias for liveness.
/// All unhealthy responses return RFC 7807 ProblemDetails.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Maps health probe endpoints on <paramref name="endpoints"/>.
    /// Call this from <c>UseSaasBuilderPipeline</c> (or directly from the host).
    /// </summary>
    public static IEndpointRouteBuilder MapSaasBuilderHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Liveness — always 200; no dependency checks.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // Skip all registered checks
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status200OK,
            },
            ResponseWriter = HealthResponseWriter.WriteAliveAsync,
        }).AllowAnonymous();

        // Readiness — database + optional RabbitMQ + optional Redis.
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
            ResponseWriter = HealthResponseWriter.WriteDetailedAsync,
        }).AllowAnonymous();

        // Startup — migrations applied flag.
        endpoints.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
            ResponseWriter = HealthResponseWriter.WriteDetailedAsync,
        }).AllowAnonymous();

        // Legacy alias — maps /health to liveness (always 200).
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status200OK,
            },
            ResponseWriter = HealthResponseWriter.WriteAliveAsync,
        }).AllowAnonymous();

        return endpoints;
    }
}
