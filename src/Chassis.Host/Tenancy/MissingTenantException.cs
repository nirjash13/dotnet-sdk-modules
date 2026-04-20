using System;

namespace Chassis.Host.Tenancy;

/// <summary>
/// Thrown by <see cref="TenantMiddleware"/> when a request arrives without a
/// resolvable tenant identity (no <c>tenant_id</c> JWT claim and no <c>X-Tenant-Id</c> header).
/// The ProblemDetails middleware maps this to HTTP 401 with <c>code=missing_tenant_claim</c>.
/// </summary>
public sealed class MissingTenantException : Exception
{
    /// <summary>Initializes a new instance of <see cref="MissingTenantException"/>.</summary>
    public MissingTenantException()
        : base("The request does not contain a resolvable tenant identity. " +
               "Provide a valid JWT with a 'tenant_id' claim, or set the 'X-Tenant-Id' header for service-account requests.")
    {
    }

    /// <summary>Initializes a new instance with a custom message.</summary>
    public MissingTenantException(string message)
        : base(message)
    {
    }
}
