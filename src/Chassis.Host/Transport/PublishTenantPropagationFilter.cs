using System.Diagnostics;
using System.Threading.Tasks;
using Chassis.SharedKernel.Contracts;
using Chassis.SharedKernel.Tenancy;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Chassis.Host.Transport;

/// <summary>
/// MassTransit publish filter that copies the ambient tenant context into outbound publish headers.
/// Mirrors <see cref="TenantPropagationSendFilter{T}"/> for the publish pipeline, which is a
/// separate pipeline in MassTransit from the send pipeline.
/// </summary>
/// <remarks>
/// When no tenant context is present, the filter passes through without setting headers.
/// </remarks>
internal sealed class PublishTenantPropagationFilter<T>(
    ITenantContextAccessor accessor,
    ILogger<PublishTenantPropagationFilter<T>> logger)
    : IFilter<PublishContext<T>>
    where T : class
{
    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant-propagation-publish");

    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        ITenantContext? tenant = accessor.Current;

        if (tenant is null)
        {
            logger.LogDebug("PublishTenantPropagationFilter: no ambient tenant context; passing through without headers.");
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

        string? traceParent = Activity.Current?.Id;
        if (traceParent is not null)
        {
            context.Headers.Set(CorrelationHeaders.TraceParent, traceParent);
        }

        return next.Send(context);
    }
}
