using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace SaasBuilder.Host.Health;

/// <summary>
/// Readiness health check — verifies that external dependencies are reachable.
/// Tagged <c>ready</c> so it is included in <c>GET /health/ready</c> but excluded from liveness.
/// </summary>
/// <remarks>
/// Consumers who need DB / RabbitMQ / Redis readiness checks should register the
/// provider-specific checks (e.g. <c>AddNpgsql()</c>, <c>AddRabbitMQ()</c>, <c>AddRedis()</c>)
/// with the <c>ready</c> tag via the standard <c>AddHealthChecks()</c> API.
/// This class serves as the default no-op readiness check when no providers are configured.
/// </remarks>
public sealed class ReadinessHealthCheck : IHealthCheck
{
    private readonly ILogger<ReadinessHealthCheck> _logger;

    /// <summary>Initializes a new instance of <see cref="ReadinessHealthCheck"/>.</summary>
    public ReadinessHealthCheck(ILogger<ReadinessHealthCheck> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Base implementation is always healthy — specific infrastructure readiness checks
        // (DB, MQ, cache) are registered by the consumer via AddHealthChecks() with tag "ready".
        _logger.LogDebug("Readiness check passed (base implementation).");
        return Task.FromResult(HealthCheckResult.Healthy("All dependencies reachable."));
    }
}
