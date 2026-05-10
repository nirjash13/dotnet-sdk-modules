using Jobs.Application.Abstractions;
using Jobs.Infrastructure.DeadLetter;
using Jobs.Infrastructure.Scheduler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobs.Infrastructure.Extensions;

/// <summary>Extension methods for registering Jobs module infrastructure services.</summary>
public static class JobsInfrastructureExtensions
{
    /// <summary>Registers all infrastructure services for the Jobs module.</summary>
    public static IServiceCollection AddJobsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register the in-process scheduler as singleton so it survives across scopes.
        services.AddSingleton<InProcessJobScheduler>();
        services.AddSingleton<IJobScheduler>(sp => sp.GetRequiredService<InProcessJobScheduler>());

        // Background worker drains the queue.
        services.AddHostedService<InProcessJobWorker>();

        // DLQ store — in-memory for Phase 5.
        services.AddSingleton<IDeadLetterQueueStore, InMemoryDeadLetterQueueStore>();

        return services;
    }
}
