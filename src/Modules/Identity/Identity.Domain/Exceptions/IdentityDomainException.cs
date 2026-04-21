using System;
using System.Runtime.Serialization;

namespace Identity.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant in the Identity bounded context is violated.
/// These are programming errors or invalid state transitions — not expected validation failures.
/// </summary>
[Serializable]
public sealed class IdentityDomainException : Exception
{
    /// <summary>Initializes a new instance with the default message.</summary>
    public IdentityDomainException()
        : base("An Identity domain invariant was violated.")
    {
    }

    /// <summary>Initializes a new instance with the supplied message.</summary>
    public IdentityDomainException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public IdentityDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Deserialization constructor.</summary>
    [Obsolete("This constructor is for serialization only and should not be used directly.")]
    private IdentityDomainException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
