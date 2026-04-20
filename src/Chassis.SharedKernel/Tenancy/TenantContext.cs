using System;
using System.Collections.Generic;

namespace Chassis.SharedKernel.Tenancy;

/// <summary>
/// Immutable snapshot of the resolved tenant context for the current ambient execution scope.
/// Created by <c>TenantMiddleware</c> and stored in <see cref="ITenantContextAccessor.Current"/>.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    /// <summary>
    /// Initializes a new <see cref="TenantContext"/>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier (from the JWT <c>tenant_id</c> claim or <c>X-Tenant-Id</c> header).</param>
    /// <param name="userId">The authenticated user identifier, or <see langword="null"/> for M2M accounts.</param>
    /// <param name="correlationId">The correlation id for distributed tracing.</param>
    /// <param name="roles">The roles assigned to the principal for this tenant.</param>
    public TenantContext(
        Guid tenantId,
        Guid? userId = null,
        string? correlationId = null,
        IReadOnlyCollection<string>? roles = null)
    {
        TenantId = tenantId;
        UserId = userId;
        CorrelationId = correlationId;
        Roles = roles ?? Array.Empty<string>();
    }

    /// <inheritdoc />
    public Guid TenantId { get; }

    /// <inheritdoc />
    public Guid? UserId { get; }

    /// <inheritdoc />
    public string? CorrelationId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles { get; }
}
