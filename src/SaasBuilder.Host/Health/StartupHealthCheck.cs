using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SaasBuilder.Host.Health;

/// <summary>
/// Startup health check — reports <see cref="HealthStatus.Healthy"/> once the host has
/// completed its startup sequence (migrations, module loading).
/// Used by the <c>GET /health/startup</c> probe and the Kubernetes startupProbe.
/// </summary>
/// <remarks>
/// Set <see cref="IsStartupComplete"/> to <c>true</c> from <c>MigrationStartupService</c>
/// (or equivalent startup code) once initialization is complete. Until then the startup probe
/// returns 503 which prevents premature traffic routing in Kubernetes.
/// </remarks>
public sealed class StartupHealthCheck : IHealthCheck
{
    // Volatile flag — set once migrations and module load are complete.
    private static volatile bool _startupComplete = false;

    /// <summary>
    /// Marks startup as complete. Call this from hosted service startup code.
    /// </summary>
    public static void MarkStartupComplete() => _startupComplete = true;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_startupComplete
            ? HealthCheckResult.Healthy("Startup complete.")
            : HealthCheckResult.Unhealthy("Startup not yet complete."));
    }
}
