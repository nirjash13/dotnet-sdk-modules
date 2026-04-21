// See Result.cs for the design-choice commentary on class vs record.

namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Represents the outcome of an operation that either succeeds with a value
/// or fails with an error message.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public sealed class Result<T>
{
    public Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the success value. Only valid when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// Accessing this on a failure result is not an error — the value will be the default for
    /// <typeparamref name="T"/> — but callers should always check <see cref="IsSuccess"/> first.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the error message when the operation failed, or <see langword="null"/> on success.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Implicitly converts a value to a successful <see cref="Result{T}"/>.
    /// Enables concise return statements: <c>return value;</c> instead of <c>return Result&lt;T&gt;.Success(value);</c>.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The success value.</param>
    public static Result<T> Success(T value) => new Result<T>(true, value, null);

    /// <summary>Creates a failed result with the given error message.</summary>
    /// <param name="error">A non-null, non-empty description of the failure.</param>
    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            throw new System.ArgumentException("Error message must not be empty.", nameof(error));
        }

        return new Result<T>(false, default, error);
    }

    /// <inheritdoc />
    public override string ToString() => IsSuccess ? $"Success({Value})" : $"Failure({Error})";
}
