using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace Jobs.Infrastructure.Schedulers;

/// <summary>
/// Hangfire-backed job scheduler. Persists jobs in PostgreSQL and dispatches them
/// via Hangfire's background processing server.
/// Each job is wrapped in a <see cref="QueuedJob"/> that carries the tenant context,
/// restored before handler invocation by <see cref="HangfireJobDispatcher"/>.
/// </summary>
public sealed class HangfireJobScheduler(
    IBackgroundJobClient backgroundJobClient,
    IRecurringJobManager recurringJobManager,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<HangfireJobScheduler> logger)
    : IJobScheduler
{
    /// <inheritdoc />
    public Task EnqueueAsync<T>(T job, CancellationToken ct = default)
        where T : IJob
    {
        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        QueuedJob queued = CaptureQueuedJob(job);
        string envelopeJson = JsonSerializer.Serialize(queued);

        backgroundJobClient.Enqueue<HangfireJobDispatcher>(
            d => d.DispatchAsync(envelopeJson, CancellationToken.None));

        logger.LogDebug(
            "Jobs.Hangfire: enqueued {Type} (key={Key})",
            typeof(T).Name, job.IdempotencyKey);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ScheduleAsync<T>(T job, DateTimeOffset runAt, CancellationToken ct = default)
        where T : IJob
    {
        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        QueuedJob queued = CaptureQueuedJob(job, runAt);
        string envelopeJson = JsonSerializer.Serialize(queued);
        TimeSpan delay = runAt - DateTimeOffset.UtcNow;

        if (delay <= TimeSpan.Zero)
        {
            backgroundJobClient.Enqueue<HangfireJobDispatcher>(
                d => d.DispatchAsync(envelopeJson, CancellationToken.None));
        }
        else
        {
            backgroundJobClient.Schedule<HangfireJobDispatcher>(
                d => d.DispatchAsync(envelopeJson, CancellationToken.None),
                delay);
        }

        logger.LogDebug(
            "Jobs.Hangfire: scheduled {Type} for {RunAt} (key={Key})",
            typeof(T).Name, runAt, job.IdempotencyKey);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ScheduleRecurringAsync<T>(
        string name,
        T job,
        string cronExpression,
        CancellationToken ct = default)
        where T : IJob
    {
        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        QueuedJob queued = CaptureQueuedJob(job);
        string envelopeJson = JsonSerializer.Serialize(queued);

        recurringJobManager.AddOrUpdate<HangfireJobDispatcher>(
            name,
            d => d.DispatchAsync(envelopeJson, CancellationToken.None),
            cronExpression);

        logger.LogDebug(
            "Jobs.Hangfire: registered recurring job '{Name}' (cron={Cron})",
            name, cronExpression);

        return Task.CompletedTask;
    }

    private QueuedJob CaptureQueuedJob<T>(T job, DateTimeOffset? runAt = null)
        where T : IJob
    {
        ITenantContext? ctx = tenantContextAccessor.Current;
        return new QueuedJob(
            JobTypeName: typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            PayloadJson: JsonSerializer.Serialize(job),
            TenantId: ctx?.TenantId ?? Guid.Empty,
            UserId: ctx?.UserId,
            CorrelationId: ctx?.CorrelationId,
            EnqueuedAt: DateTimeOffset.UtcNow,
            RunAt: runAt);
    }
}
