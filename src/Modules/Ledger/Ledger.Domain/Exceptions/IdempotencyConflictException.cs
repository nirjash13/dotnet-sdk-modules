using System;
using System.Runtime.Serialization;

namespace Ledger.Domain.Exceptions;

/// <summary>
/// Thrown by the infrastructure layer when a duplicate idempotency key is detected.
/// The Application layer catches this and returns a successful idempotency-replay result.
/// </summary>
[Serializable]
public sealed class IdempotencyConflictException : Exception
{
    /// <summary>Initializes a new instance with the default message.</summary>
    public IdempotencyConflictException()
        : base("A posting with this idempotency key already exists.")
    {
    }

    /// <summary>Initializes a new instance with an existing posting Id.</summary>
    public IdempotencyConflictException(Guid idempotencyKey)
        : base($"A posting with idempotency key '{idempotencyKey}' already exists.")
    {
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Initializes a new instance with an idempotency key and inner exception.</summary>
    public IdempotencyConflictException(Guid idempotencyKey, Exception innerException)
        : base($"A posting with idempotency key '{idempotencyKey}' already exists.", innerException)
    {
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Initializes a new instance with a message.</summary>
    public IdempotencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public IdempotencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Deserialization constructor.</summary>
    [Obsolete("This constructor is for serialization only and should not be used directly.")]
    private IdempotencyConflictException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    /// <summary>Gets the idempotency key that caused the conflict.</summary>
    public Guid IdempotencyKey { get; }
}
