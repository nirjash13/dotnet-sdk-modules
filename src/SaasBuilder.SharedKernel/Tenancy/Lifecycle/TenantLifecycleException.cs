using System;

namespace SaasBuilder.SharedKernel.Tenancy.Lifecycle;

/// <summary>
/// Thrown when a <see cref="ITenantLifecycleHandler"/> fails during a lifecycle transition.
/// The <see cref="Exception.InnerException"/> preserves the original handler exception.
/// </summary>
public sealed class TenantLifecycleException : Exception
{
    /// <summary>Initializes a new instance of <see cref="TenantLifecycleException"/>.</summary>
    public TenantLifecycleException()
        : base("A tenant lifecycle transition failed.")
    {
    }

    /// <summary>Initializes a new instance of <see cref="TenantLifecycleException"/> with a message.</summary>
    public TenantLifecycleException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of <see cref="TenantLifecycleException"/> with a message and inner exception.</summary>
    public TenantLifecycleException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TenantLifecycleException"/>.
    /// </summary>
    /// <param name="tenantId">The tenant involved in the failed transition.</param>
    /// <param name="operation">The lifecycle operation (e.g., "Provision", "Suspend").</param>
    /// <param name="handlerTypeName">The full name of the failing handler type.</param>
    /// <param name="inner">The original exception thrown by the handler.</param>
    public TenantLifecycleException(
        Guid tenantId,
        string operation,
        string handlerTypeName,
        Exception inner)
        : base(
            $"Lifecycle handler '{handlerTypeName}' failed during '{operation}' for tenant {tenantId}.",
            inner)
    {
        TenantId = tenantId;
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        HandlerTypeName = handlerTypeName ?? throw new ArgumentNullException(nameof(handlerTypeName));
    }

    /// <summary>Gets the tenant identifier involved in the failed transition.</summary>
    public Guid TenantId { get; }

    /// <summary>Gets the lifecycle operation that failed.</summary>
    public string? Operation { get; }

    /// <summary>Gets the name of the handler type that threw.</summary>
    public string? HandlerTypeName { get; }
}
