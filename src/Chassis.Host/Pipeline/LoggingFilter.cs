using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Chassis.Host.Observability;
using Chassis.SharedKernel.Tenancy;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Chassis.Host.Pipeline;

/// <summary>
/// MassTransit consume filter that logs command type, tenant id, correlation id, and duration.
/// Positioned first in the pipeline so it wraps all other filters and captures total latency.
/// </summary>
internal sealed class LoggingFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ILogger<LoggingFilter<T>> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public LoggingFilter(ILogger<LoggingFilter<T>> logger, ITenantContextAccessor tenantContextAccessor)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("logging");
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        string commandType = typeof(T).Name;
        Guid? tenantId = _tenantContextAccessor.Current?.TenantId;
        string? correlationId = _tenantContextAccessor.Current?.CorrelationId
                             ?? context.CorrelationId?.ToString();

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Dispatch start: {CommandType} | Tenant={TenantId} | CorrelationId={CorrelationId}",
            commandType,
            tenantId?.ToString() ?? "none",
            correlationId ?? "none");

        try
        {
            await next.Send(context).ConfigureAwait(false);
            sw.Stop();

            var tags = new System.Diagnostics.TagList
            {
                { "command", commandType },
                { "transport", "inproc" },
            };
            ChassisMeters.CommandDuration.Record(sw.Elapsed.TotalSeconds, tags);

            _logger.LogInformation(
                "Dispatch complete: {CommandType} in {DurationMs:F2}ms | Tenant={TenantId}",
                commandType,
                sw.Elapsed.TotalMilliseconds,
                tenantId?.ToString() ?? "none");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "Dispatch failed: {CommandType} in {DurationMs:F2}ms | Tenant={TenantId}",
                commandType,
                sw.Elapsed.TotalMilliseconds,
                tenantId?.ToString() ?? "none");
            throw;
        }
    }
}
