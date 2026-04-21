using System;
using System.Threading.Tasks;
using Chassis.Host.Tenancy;
using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using MassTransit;

namespace Chassis.Host.Pipeline;

/// <summary>
/// MassTransit consume filter that enforces tenant context presence for any message
/// implementing <see cref="ICommand"/> or <see cref="IQuery{TResponse}"/>.
/// </summary>
/// <remarks>
/// Non-command/query message types (e.g., integration events published on the bus) are
/// allowed through without a tenant check — those flows re-hydrate tenant context from
/// message headers in their own consumers.
/// </remarks>
internal sealed class TenantFilter<T> : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantFilter(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor
            ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("tenant");
    }

    public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        Type messageType = typeof(T);
        bool requiresTenant = IsCommand(messageType) || IsQuery(messageType);

        if (requiresTenant && _tenantContextAccessor.Current is null)
        {
            throw new MissingTenantException(
                $"No ambient tenant context when dispatching '{messageType.Name}'. " +
                "TenantMiddleware must set ITenantContextAccessor.Current before mediator dispatch.");
        }

        return next.Send(context);
    }

    private static bool IsCommand(Type type)
    {
        // Check both non-generic ICommand and generic ICommand<TResponse>.
        if (typeof(ICommand).IsAssignableFrom(type))
        {
            return true;
        }

        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.IsGenericType &&
                iface.GetGenericTypeDefinition() == typeof(ICommand<>))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQuery(Type type)
    {
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.IsGenericType &&
                iface.GetGenericTypeDefinition() == typeof(IQuery<>))
            {
                return true;
            }
        }

        return false;
    }
}
