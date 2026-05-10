using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jobs.Application.Abstractions;

/// <summary>
/// Schedules and enqueues background jobs.
/// All methods carry tenant context through <c>JobEnvelope</c> — the current
/// <c>ITenantContextAccessor</c> value is captured at enqueue time and restored
/// before handler invocation.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Enqueues a job for immediate execution.</summary>
    /// <typeparam name="T">The job type.</typeparam>
    /// <param name="job">The job payload. Must have a non-empty <see cref="IJob.IdempotencyKey"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnqueueAsync<T>(T job, CancellationToken ct = default)
        where T : IJob;

    /// <summary>Schedules a job to run at a specific time.</summary>
    Task ScheduleAsync<T>(T job, DateTimeOffset runAt, CancellationToken ct = default)
        where T : IJob;

    /// <summary>Registers or updates a named recurring job with a cron expression.</summary>
    /// <param name="name">Stable name for the recurring job (used for update/cancellation).</param>
    /// <param name="job">The job prototype enqueued on each trigger.</param>
    /// <param name="cronExpression">Standard cron expression (e.g. <c>"0 * * * *"</c> = every hour).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ScheduleRecurringAsync<T>(string name, T job, string cronExpression, CancellationToken ct = default)
        where T : IJob;
}
