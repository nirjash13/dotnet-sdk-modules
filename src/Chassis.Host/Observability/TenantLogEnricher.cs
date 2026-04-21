using System;
using Chassis.SharedKernel.Tenancy;
using Serilog.Core;
using Serilog.Events;

namespace Chassis.Host.Observability;

/// <summary>
/// Serilog enricher that stamps tenant-scoped properties onto every log record.
/// Properties written: <c>tenant_id</c>, <c>user_id</c>, <c>correlation_id</c>.
/// Values are absent (not set) when no tenant context has been established for the
/// current execution scope (e.g. in background services or before TenantMiddleware runs).
/// </summary>
/// <remarks>
/// Registered in the Serilog pipeline via <c>.Enrich.With&lt;TenantLogEnricher&gt;()</c>.
/// Resolved from DI so it can read the singleton <see cref="ITenantContextAccessor"/>.
/// </remarks>
internal sealed class TenantLogEnricher : ILogEventEnricher
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantLogEnricher(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor
            ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ITenantContext? ctx = _tenantContextAccessor.Current;

        if (ctx is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("tenant_id", ctx.TenantId.ToString()));

        if (ctx.UserId.HasValue)
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("user_id", ctx.UserId.Value.ToString()));
        }

        if (ctx.CorrelationId is { Length: > 0 })
        {
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("correlation_id", ctx.CorrelationId));
        }
    }
}
