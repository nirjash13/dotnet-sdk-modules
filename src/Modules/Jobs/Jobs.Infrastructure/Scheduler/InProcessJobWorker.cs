using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace Jobs.Infrastructure.Scheduler;

/// <summary>
/// <see cref="BackgroundService"/> that drains the <see cref="InProcessJobScheduler"/> queue.
/// Restores tenant context from the job envelope before invoking the handler.
/// </summary>
internal sealed class InProcessJobWorker(
    InProcessJobScheduler scheduler,
    ILogger<InProcessJobWorker> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Jobs.InProcessWorker: started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (scheduler.Queue.TryDequeue(out QueuedJob? job))
            {
                // Respect scheduled run-at time.
                if (job.RunAt.HasValue && job.RunAt.Value > DateTimeOffset.UtcNow)
                {
                    // Re-queue and continue — simple polling approach for Phase 5.
                    scheduler.Queue.Enqueue(job);
                    await Task.Delay(100, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await DispatchJobAsync(job, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Delay(50, stoppingToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("Jobs.InProcessWorker: stopping.");
    }

    private async Task DispatchJobAsync(QueuedJob job, CancellationToken ct)
    {
        using IServiceScope scope = scheduler.ServiceProvider.CreateScope();

        // Restore tenant context before handler invocation.
        ITenantContextAccessor tenantAccessor =
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

        if (job.TenantId != Guid.Empty)
        {
            tenantAccessor.Current = new RestoredTenantContext(
                job.TenantId, job.UserId, job.CorrelationId);
        }

        Type? jobType = Type.GetType(job.JobTypeName);
        if (jobType is null)
        {
            logger.LogError("Jobs.InProcessWorker: cannot resolve type '{TypeName}'. Job skipped.", job.JobTypeName);
            return;
        }

        object? payload = JsonSerializer.Deserialize(job.PayloadJson, jobType);
        if (payload is null)
        {
            logger.LogError("Jobs.InProcessWorker: failed to deserialize job payload for '{TypeName}'.", job.JobTypeName);
            return;
        }

        Type handlerInterface = typeof(IJobHandler<>).MakeGenericType(jobType);
        object? handler = scope.ServiceProvider.GetService(handlerInterface);

        if (handler is null)
        {
            logger.LogWarning(
                "Jobs.InProcessWorker: no IJobHandler<{Type}> registered. Dropping job '{Key}'.",
                jobType.Name, ((IJob)payload).IdempotencyKey);
            return;
        }

        try
        {
            System.Reflection.MethodInfo? method = handlerInterface.GetMethod("HandleAsync");
            if (method is null)
            {
                return;
            }

            object? task = method.Invoke(handler, new[] { payload, (object)ct });
            if (task is Task t)
            {
                await t.ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Jobs.InProcessWorker: handler for '{Type}' threw an exception.", jobType.Name);
        }
    }

    /// <summary>Minimal tenant context restored from a job envelope.</summary>
    private sealed class RestoredTenantContext(Guid tenantId, Guid? userId, string? correlationId)
        : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId { get; } = correlationId;
        public IReadOnlyCollection<string> Roles { get; } = System.Array.Empty<string>();
    }
}
