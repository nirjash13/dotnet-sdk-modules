using System.Diagnostics;
using System.Threading.Tasks;
using Chassis.SharedKernel.Contracts;
using Chassis.SharedKernel.Tenancy;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Chassis.Host.Transport;

/// <summary>
/// MassTransit send filter that copies the ambient tenant context into outbound message headers.
/// Downstream consumers rehydrate the context via <see cref="TenantPropagationConsumeFilter{T}"/>.
/// </summary>
/// <remarks>
/// When no tenant context is present (anonymous or system-initiated dispatch), the filter passes
/// the message through without setting headers. <c>TenantFilter</c> handles rejection for
/// commands that require a tenant context.
/// </remarks>
internal sealed class TenantPropagationSendFilter<T>(
    ITenantContextAccessor accessor,
    ILogger<TenantPropagationSendFilter<T>> logger)
    : IFilter<SendContext<T>>
    where T : class
{
    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant-propagation-send");

    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        ITenantContext? tenant = accessor.Current;

        if (tenant is null)
        {
            logger.LogDebug("TenantPropagationSendFilter: no ambient tenant context; passing through without headers.");
            return next.Send(context);
        }

        context.Headers.Set(CorrelationHeaders.TenantId, tenant.TenantId.ToString());

        if (tenant.UserId.HasValue)
        {
            context.Headers.Set(CorrelationHeaders.UserId, tenant.UserId.Value.ToString());
        }

        if (tenant.CorrelationId is not null)
        {
            context.Headers.Set(CorrelationHeaders.CorrelationId, tenant.CorrelationId);
        }

        // Propagate W3C traceparent from the active OTel span if available.
        string? traceParent = Activity.Current?.Id;
        if (traceParent is not null)
        {
            context.Headers.Set(CorrelationHeaders.TraceParent, traceParent);
        }

        return next.Send(context);
    }
}
