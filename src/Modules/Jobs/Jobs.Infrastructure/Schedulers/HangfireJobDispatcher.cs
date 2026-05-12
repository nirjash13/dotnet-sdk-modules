using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace Jobs.Infrastructure.Schedulers;

/// <summary>
/// Invoked by Hangfire to dispatch a job. Resolves the concrete handler from DI
/// and invokes it with the deserialized job payload.
/// The job envelope is passed as JSON string so the internal <see cref="QueuedJob"/>
/// type does not appear in a public method signature.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant context (C-3):</b> The envelope carries the <c>TenantId</c> captured at enqueue
/// time by <see cref="HangfireJobScheduler"/>. Before invoking the handler this dispatcher
/// pushes a synthetic <see cref="ITenantContext"/> into <see cref="ITenantContextAccessor.Current"/>
/// so all downstream code (EF Core global query filters, RLS interceptor, audit logs) sees the
/// correct tenant for the duration of the job.
/// </para>
/// <para>
/// <b>Type-allowlist (C-4):</b> Job type resolution goes through <see cref="IJobTypeRegistry"/>
/// rather than bare <c>Type.GetType</c>. This prevents an attacker who can write to the Hangfire
/// job tables from triggering deserialization of arbitrary CLR types.
/// </para>
/// </remarks>
public sealed class HangfireJobDispatcher(
    IServiceProvider serviceProvider,
    IJobTypeRegistry jobTypeRegistry,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<HangfireJobDispatcher> logger)
{
    /// <summary>Deserializes the queued job envelope JSON and invokes the registered handler.</summary>
    /// <param name="queuedJobJson">JSON serialization of <see cref="QueuedJob"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DispatchAsync(string queuedJobJson, CancellationToken ct)
    {
        QueuedJob? queued = JsonSerializer.Deserialize<QueuedJob>(queuedJobJson);
        if (queued is null)
        {
            logger.LogError("Jobs.Hangfire: failed to deserialize QueuedJob envelope.");
            return;
        }

        // C-4: resolve type through the allowlist — never via bare Type.GetType.
        Type? jobType = jobTypeRegistry.Resolve(queued.JobTypeName);
        if (jobType is null)
        {
            logger.LogError(
                "Jobs.Hangfire: job type '{TypeName}' is not in the registered allowlist — handler not dispatched.",
                queued.JobTypeName);
            return;
        }

        object? job = JsonSerializer.Deserialize(queued.PayloadJson, jobType);
        if (job is null)
        {
            logger.LogError(
                "Jobs.Hangfire: failed to deserialize payload for type '{TypeName}'.",
                queued.JobTypeName);
            return;
        }

        // C-3: restore tenant context captured at enqueue time before invoking the handler.
        // Without this, every background job would run with tenantId == Guid.Empty, causing
        // EF Core global query filters and the RLS interceptor to return no rows.
        using IDisposable? tenantScope = queued.TenantId != Guid.Empty
            ? PushTenantContext(queued)
            : null;

        Type handlerType = typeof(IJobHandler<>).MakeGenericType(jobType);
        object? handler = serviceProvider.GetService(handlerType);

        if (handler is null)
        {
            logger.LogWarning(
                "Jobs.Hangfire: no handler registered for job type '{TypeName}'. Skipping.",
                queued.JobTypeName);
            return;
        }

        System.Reflection.MethodInfo? executeMethod = handlerType.GetMethod("ExecuteAsync");
        if (executeMethod is null)
        {
            logger.LogError(
                "Jobs.Hangfire: IJobHandler<{TypeName}>.ExecuteAsync not found.",
                queued.JobTypeName);
            return;
        }

        try
        {
            object? result = executeMethod.Invoke(handler, new[] { job, ct });
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
            }

            logger.LogInformation(
                "Jobs.Hangfire: dispatched {TypeName} (tenantId={TenantId})",
                queued.JobTypeName, queued.TenantId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Jobs.Hangfire: handler for '{TypeName}' threw an exception.",
                queued.JobTypeName);
            throw; // Let Hangfire handle retries.
        }
    }

    /// <summary>
    /// Pushes a tenant context synthesized from the captured envelope into
    /// <see cref="ITenantContextAccessor.Current"/> and returns a scope handle that
    /// restores the previous context on dispose.
    /// </summary>
    private IDisposable PushTenantContext(QueuedJob queued)
    {
        ITenantContext? previous = tenantContextAccessor.Current;
        tenantContextAccessor.Current = new BackgroundJobTenantContext(
            queued.TenantId,
            queued.UserId,
            queued.CorrelationId);
        return new RestoreContextScope(tenantContextAccessor, previous);
    }

    /// <summary>Synthetic tenant context reconstructed from a job envelope at dequeue time.</summary>
    private sealed class BackgroundJobTenantContext(Guid tenantId, Guid? userId, string? correlationId)
        : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;

        public Guid? UserId { get; } = userId;

        public string? CorrelationId { get; } = correlationId;

        public IReadOnlyCollection<string> Roles { get; } = Array.Empty<string>();
    }

    /// <summary>
    /// Restores the previous <see cref="ITenantContextAccessor.Current"/> on dispose.
    /// </summary>
    private sealed class RestoreContextScope(
        ITenantContextAccessor accessor,
        ITenantContext? previous) : IDisposable
    {
        public void Dispose() => accessor.Current = previous;
    }
}
