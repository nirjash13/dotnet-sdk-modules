using System;

namespace SaasBuilder.SharedKernel.Tenancy.Resolution;

/// <summary>
/// Thrown when the tenant resolver pipeline cannot determine the tenant for a request
/// and no anonymous bypass is applicable. The middleware converts this to HTTP 401.
/// </summary>
public sealed class TenantResolutionException : Exception
{
    /// <summary>Initializes a new instance of <see cref="TenantResolutionException"/>.</summary>
    public TenantResolutionException()
        : base("Tenant could not be resolved for the current request.")
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TenantResolutionException"/> with a
    /// descriptive message about why resolution failed.
    /// </summary>
    /// <param name="message">Human-readable description of the resolution failure.</param>
    public TenantResolutionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TenantResolutionException"/> with a
    /// message and an inner exception.
    /// </summary>
    /// <param name="message">Human-readable description of the resolution failure.</param>
    /// <param name="inner">The underlying exception.</param>
    public TenantResolutionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
