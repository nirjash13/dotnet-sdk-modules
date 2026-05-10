using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace Jobs.Infrastructure.Scheduler;

/// <summary>
/// In-process job scheduler backed by a <see cref="ConcurrentQueue{T}"/>.
/// A <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> drains the queue
/// and dispatches to registered <see cref="IJobHandler{TJob}"/> implementations.
/// Tenant context is captured at enqueue time and restored before handler invocation.
/// </summary>
public sealed class InProcessJobScheduler(
    IServiceProvider serviceProvider,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<InProcessJobScheduler> logger)
    : IJobScheduler
{
    // Untyped envelope queue — stores JSON-serialized payloads with type info.
    internal readonly ConcurrentQueue<QueuedJob> Queue = new();

    // Idempotency set — persists idempotency keys of enqueued jobs.
    // TODO(Phase 5.3): replace with DB-backed idempotency store.
    private readonly HashSet<string> _processedKeys = new(StringComparer.Ordinal);
    private readonly object _keysLock = new();

    /// <inheritdoc />
    public Task EnqueueAsync<T>(T job, CancellationToken ct = default)
        where T : IJob
    {
        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        lock (_keysLock)
        {
            if (!_processedKeys.Add(job.IdempotencyKey))
            {
                logger.LogInformation(
                    "Jobs.InProcess: skipping duplicate enqueue for idempotency key '{Key}' (type={Type})",
                    job.IdempotencyKey, typeof(T).Name);
                return Task.CompletedTask;
            }
        }

        ITenantContext? ctx = tenantContextAccessor.Current;
        QueuedJob queuedJob = new QueuedJob(
            JobTypeName: typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            PayloadJson: JsonSerializer.Serialize(job),
            TenantId: ctx?.TenantId ?? Guid.Empty,
            UserId: ctx?.UserId,
            CorrelationId: ctx?.CorrelationId,
            EnqueuedAt: DateTimeOffset.UtcNow,
            RunAt: null);

        Queue.Enqueue(queuedJob);
        logger.LogDebug(
            "Jobs.InProcess: enqueued {Type} (key={Key})",
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

        ITenantContext? ctx = tenantContextAccessor.Current;
        QueuedJob queuedJob = new QueuedJob(
            JobTypeName: typeof(T).AssemblyQualifiedName ?? typeof(T).Name,
            PayloadJson: JsonSerializer.Serialize(job),
            TenantId: ctx?.TenantId ?? Guid.Empty,
            UserId: ctx?.UserId,
            CorrelationId: ctx?.CorrelationId,
            EnqueuedAt: DateTimeOffset.UtcNow,
            RunAt: runAt);

        Queue.Enqueue(queuedJob);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ScheduleRecurringAsync<T>(string name, T job, string cronExpression, CancellationToken ct = default)
        where T : IJob
    {
        // TODO(Phase 5.3): implement cron scheduling via Quartz.NET or Hangfire.
        // For Phase 5 scaffold, recurring jobs are not supported in the in-process scheduler.
        logger.LogWarning(
            "Jobs.InProcess: recurring job '{Name}' (cron={Cron}) registered but NOT scheduled. " +
            "TODO(Phase 5.3): install Hangfire.PostgreSql or Quartz.NET for recurring job support.",
            name, cronExpression);

        return Task.CompletedTask;
    }

    /// <summary>Exposes the service provider for the background worker to resolve handlers.</summary>
    internal IServiceProvider ServiceProvider => serviceProvider;
}
