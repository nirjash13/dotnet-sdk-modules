namespace Jobs.Application.Abstractions;

/// <summary>
/// Marker interface for all job payloads.
/// Every type passed to <see cref="IJobScheduler"/> must implement this interface.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Gets the idempotency key. Two enqueue calls with the same key produce exactly
    /// one execution. The scheduler enforces uniqueness by persisting processed keys.
    /// </summary>
    string IdempotencyKey { get; }
}
