using System;
using System.Runtime.Serialization;

namespace Ledger.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant in the Ledger bounded context is violated.
/// These are programming errors or invalid state transitions — not expected validation failures.
/// </summary>
[Serializable]
public sealed class LedgerDomainException : Exception
{
    /// <summary>Initializes a new instance with the default message.</summary>
    public LedgerDomainException()
        : base("A Ledger domain invariant was violated.")
    {
    }

    /// <summary>Initializes a new instance with the supplied message.</summary>
    public LedgerDomainException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public LedgerDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Deserialization constructor.</summary>
    [Obsolete("This constructor is for serialization only and should not be used directly.")]
    private LedgerDomainException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
