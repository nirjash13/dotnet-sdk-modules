using System;
using System.Threading;
using System.Threading.Tasks;
using Jobs.Application.Abstractions;

namespace Jobs.Infrastructure.Scheduler;

// ---------------------------------------------------------------------------
// Third-party scheduler stubs — Phase 5.3
// ---------------------------------------------------------------------------

/// <summary>
/// TODO(Phase 5.3): Hangfire job scheduler.
/// Install Hangfire.PostgreSql + Hangfire.AspNetCore NuGet packages.
/// Verify Hangfire license (LGPL / commercial) for your deployment model.
/// </summary>
internal sealed class HangfireJobScheduler : IJobScheduler
{
    public Task EnqueueAsync<T>(T job, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): Hangfire.PostgreSql integration");

    public Task ScheduleAsync<T>(T job, DateTimeOffset runAt, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): Hangfire.PostgreSql integration");

    public Task ScheduleRecurringAsync<T>(string name, T job, string cronExpression, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): Hangfire.PostgreSql integration");
}

/// <summary>TODO(Phase 5.3): Quartz.NET scheduler — install Quartz + Quartz.Extensions.Hosting.</summary>
internal sealed class QuartzJobScheduler : IJobScheduler
{
    public Task EnqueueAsync<T>(T job, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): Quartz.NET integration");

    public Task ScheduleAsync<T>(T job, DateTimeOffset runAt, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): Quartz.NET integration");

    public Task ScheduleRecurringAsync<T>(string name, T job, string cronExpression, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): Quartz.NET integration");
}

/// <summary>TODO(Phase 5.3): MassTransit scheduled job scheduler — uses MT message scheduling.</summary>
internal sealed class MassTransitScheduledJobScheduler : IJobScheduler
{
    public Task EnqueueAsync<T>(T job, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): MassTransit scheduled job integration");

    public Task ScheduleAsync<T>(T job, DateTimeOffset runAt, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): MassTransit scheduled job integration");

    public Task ScheduleRecurringAsync<T>(string name, T job, string cronExpression, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.3): MassTransit scheduled job integration");
}
