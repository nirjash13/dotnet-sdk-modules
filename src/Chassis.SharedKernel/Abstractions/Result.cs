// Result pattern implementation for Chassis.SharedKernel.
//
// Design choice: using sealed classes with private constructors rather than
// record types with init-only setters. Records with `init` require the
// IsExternalInit polyfill on netstandard2.0 (which adds noise); a simple
// class achieves the same immutability guarantee cleanly on both targets.

namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Represents the outcome of an operation that either succeeds or fails with an error message.
/// Use this type for operations that produce no value on success.
/// For operations that produce a value, use <see cref="Result{T}"/>.
/// </summary>
public sealed class Result
{
    private static readonly Result SuccessInstance = new Result(true, null);

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message when the operation failed, or <see langword="null"/> on success.
    /// </summary>
    public string? Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => SuccessInstance;

    /// <summary>Creates a failed result with the given error message.</summary>
    /// <param name="error">A non-null, non-empty description of the failure.</param>
    public static Result Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            // Defensive: a failure must carry a meaningful error.
            throw new System.ArgumentException("Error message must not be empty.", nameof(error));
        }

        return new Result(false, error);
    }

    /// <inheritdoc />
    public override string ToString() => IsSuccess ? "Success" : $"Failure({Error})";
}
