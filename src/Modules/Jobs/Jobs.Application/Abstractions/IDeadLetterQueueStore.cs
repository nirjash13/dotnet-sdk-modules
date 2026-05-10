using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jobs.Application.Abstractions;

/// <summary>
/// Persists jobs that have exhausted all retry attempts.
/// </summary>
public interface IDeadLetterQueueStore
{
    /// <summary>Persists a dead-lettered job payload.</summary>
    /// <param name="jobType">Fully qualified type name of the job.</param>
    /// <param name="payloadJson">Serialized job payload.</param>
    /// <param name="tenantId">The tenant that owned the job at enqueue time.</param>
    /// <param name="error">The final exception message.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(
        string jobType,
        string payloadJson,
        Guid tenantId,
        string error,
        CancellationToken ct = default);
}
