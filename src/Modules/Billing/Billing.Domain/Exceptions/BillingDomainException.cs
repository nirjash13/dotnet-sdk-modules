using System;

namespace Billing.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant in the Billing bounded context is violated.
/// Callers should catch this at application-layer boundaries and convert to Result.Failure.
/// </summary>
public sealed class BillingDomainException : Exception
{
    /// <summary>Initializes a new instance with the specified message.</summary>
    public BillingDomainException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public BillingDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
