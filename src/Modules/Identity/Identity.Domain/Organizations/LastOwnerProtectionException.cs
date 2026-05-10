using System;
using System.Runtime.Serialization;
using Identity.Domain.Exceptions;

namespace Identity.Domain.Organizations;

/// <summary>
/// Thrown when an operation would leave an <see cref="Organization"/> with no Owner.
/// Every organization must have at least one active Owner at all times.
/// </summary>
[Serializable]
public sealed class LastOwnerProtectionException : IdentityDomainException
{
    /// <summary>Initializes a new instance with the standard protection message.</summary>
    public LastOwnerProtectionException()
        : base("Organization must always have at least one active Owner. Remove the last Owner by transferring ownership first.")
    {
    }

    /// <summary>Initializes a new instance with a custom message.</summary>
    public LastOwnerProtectionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public LastOwnerProtectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Deserialization constructor.</summary>
    [Obsolete("This constructor is for serialization only and should not be used directly.")]
    private LastOwnerProtectionException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
