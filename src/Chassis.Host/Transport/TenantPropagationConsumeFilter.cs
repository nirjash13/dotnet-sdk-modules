using System;
using System.Threading.Tasks;
using Chassis.SharedKernel.Contracts;
using Chassis.SharedKernel.Tenancy;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Chassis.Host.Transport;

/// <summary>
/// MassTransit consume filter that rehydrates the ambient tenant context from inbound message headers.
/// Must be the outermost consume filter so all downstream filters (Logging, Tenant, Validation,
/// Transaction) execute within an established tenant context.
/// </summary>
/// <remarks>
/// When headers are absent (in-proc calls without tenant context, or system events), the filter
/// passes the message through without setting context. Rejection for commands that require a
/// tenant is the responsibility of <c>TenantFilter</c>.
/// </remarks>
internal sealed class TenantPropagationConsumeFilter<T>(
    ITenantContextAccessor accessor,
    ILogger<TenantPropagationConsumeFilter<T>> logger)
    : IFilter<ConsumeContext<T>>
    where T : class
{
    public void Probe(ProbeContext context) => context.CreateFilterScope("tenant-propagation-consume");

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        string? tenantIdRaw = context.Headers.Get<string>(CorrelationHeaders.TenantId);

        if (tenantIdRaw is null || !Guid.TryParse(tenantIdRaw, out Guid tenantId))
        {
            logger.LogDebug(
                "TenantPropagationConsumeFilter: header '{Header}' absent or unparseable; passing through without tenant context.",
                CorrelationHeaders.TenantId);

            await next.Send(context).ConfigureAwait(false);
            return;
        }

        string? userIdRaw = context.Headers.Get<string>(CorrelationHeaders.UserId);
        Guid? userId = Guid.TryParse(userIdRaw, out Guid parsed) ? parsed : null;

        string? correlationId = context.Headers.Get<string>(CorrelationHeaders.CorrelationId)
                             ?? context.CorrelationId?.ToString();

        logger.LogDebug(
            "TenantPropagationConsumeFilter: rehydrating tenant context. TenantId={TenantId} UserId={UserId} CorrelationId={CorrelationId}",
            tenantId,
            userId,
            correlationId ?? "none");

        // Capture any pre-existing context so we can restore it after dispatch.
        // In practice this will be null for consumer invocations, but guard defensively.
        ITenantContext? previous = accessor.Current;

        accessor.Current = new TenantContext(tenantId, userId, correlationId);

        try
        {
            await next.Send(context).ConfigureAwait(false);
        }
        finally
        {
            // Null out on exit to prevent context leaking across consumer dispatches that
            // share the same async execution context (MassTransit may reuse the context
            // across sequential message dispatches on the same consumer instance).
            accessor.Current = previous;
        }
    }
}
