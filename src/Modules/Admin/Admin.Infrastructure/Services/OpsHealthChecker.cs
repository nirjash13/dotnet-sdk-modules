using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Contracts;
using Admin.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Admin.Infrastructure.Services;

/// <summary>
/// Checks operational health of the platform by probing DB, queue, and registered health checks.
/// </summary>
public sealed class OpsHealthChecker(
    AdminDbContext dbContext,
    HealthCheckService healthCheckService,
    ILogger<OpsHealthChecker> logger) : IOpsHealthChecker
{
    /// <inheritdoc />
    public async Task<OpsHealthDto> CheckAsync(CancellationToken ct = default)
    {
        ComponentStatus dbStatus = await PingDatabaseAsync(ct).ConfigureAwait(false);

        HealthReport? report = null;
        try
        {
            report = await healthCheckService
                .CheckHealthAsync(ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check service failed.");
        }

        ComponentStatus queueStatus = ComponentStatus.Unknown;
        List<ProviderHealthDto> providers = new List<ProviderHealthDto>();

        if (report is not null)
        {
            foreach ((string name, HealthReportEntry entry) in report.Entries)
            {
                ComponentStatus componentStatus = entry.Status switch
                {
                    HealthStatus.Healthy => ComponentStatus.Healthy,
                    HealthStatus.Degraded => ComponentStatus.Degraded,
                    _ => ComponentStatus.Unhealthy,
                };

                if (name.Contains("queue", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("rabbit", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("masstransit", StringComparison.OrdinalIgnoreCase))
                {
                    queueStatus = componentStatus;
                }
                else if (!name.Contains("db", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("database", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("npgsql", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    providers.Add(new ProviderHealthDto
                    {
                        Name = name,
                        Status = componentStatus,
                        LatencyMs = entry.Duration.TotalMilliseconds,
                    });
                }
            }
        }

        ComponentStatus overall = dbStatus == ComponentStatus.Unhealthy
            ? ComponentStatus.Unhealthy
            : ComponentStatus.Healthy;

        return new OpsHealthDto
        {
            DbStatus = dbStatus,
            QueueStatus = queueStatus,
            Providers = providers,
            SloStatus = ComponentStatus.Unknown,
            Overall = overall,
        };
    }

    private async Task<ComponentStatus> PingDatabaseAsync(CancellationToken ct)
    {
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            bool canConnect = await dbContext.Database.CanConnectAsync(ct).ConfigureAwait(false);
            sw.Stop();

            if (!canConnect)
            {
                return ComponentStatus.Unhealthy;
            }

            return sw.Elapsed.TotalMilliseconds > 1000
                ? ComponentStatus.Degraded
                : ComponentStatus.Healthy;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database ping failed.");
            return ComponentStatus.Unhealthy;
        }
    }
}
