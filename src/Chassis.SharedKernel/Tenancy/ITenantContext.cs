using System;
using System.Collections.Generic;

namespace Chassis.SharedKernel.Tenancy;

/// <summary>
/// Immutable snapshot of the tenant context for the current ambient execution scope.
/// Populated by <c>TenantMiddleware</c> from the validated JWT claims and stored
/// in <see cref="ITenantContextAccessor.Current"/>.
/// </summary>
public interface ITenantContext
{
    /// <summary>Gets the tenant identifier extracted from the JWT <c>tenant_id</c> claim.</summary>
    Guid TenantId { get; }

    /// <summary>
    /// Gets the authenticated user identifier extracted from the JWT <c>sub</c> claim,
    /// or <see langword="null"/> for machine-to-machine service account requests.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Gets the correlation identifier for distributed tracing.
    /// Sourced from the <c>correlation-id</c> header; generated if absent.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>Gets the roles assigned to the authenticated principal for this tenant.</summary>
    IReadOnlyCollection<string> Roles { get; }
}
