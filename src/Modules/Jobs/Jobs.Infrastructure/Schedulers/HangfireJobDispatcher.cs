using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jobs.Application.Abstractions;
using Jobs.Infrastructure.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobs.Infrastructure.Schedulers;

/// <summary>
/// Invoked by Hangfire to dispatch a job. Resolves the concrete handler from DI
/// and invokes it with the deserialized job payload.
/// The job envelope is passed as JSON string so the internal <see cref="QueuedJob"/>
/// type does not appear in a public method signature.
/// </summary>
public sealed class HangfireJobDispatcher(
    IServiceProvider serviceProvider,
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

        Type? jobType = Type.GetType(queued.JobTypeName);
        if (jobType is null)
        {
            logger.LogError(
                "Jobs.Hangfire: cannot resolve job type '{TypeName}' — handler not dispatched.",
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
}
