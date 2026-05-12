// Stub schedulers removed in Phase 5.3 — real implementations are in Jobs.Infrastructure.Schedulers namespace.
// QuartzJobScheduler and MassTransitScheduledJobScheduler remain as future placeholders (not registered).
using System;
using System.Threading;
using System.Threading.Tasks;
using Jobs.Application.Abstractions;

namespace Jobs.Infrastructure.Scheduler;

/// <summary>
/// TODO(Phase 5.x): Quartz.NET scheduler — install Quartz + Quartz.Extensions.Hosting.
/// TODO(M-O4): stub — register conditionally only when adapter implemented
///   (gate on <c>SaasBuilder:Schedulers:Quartz:Enabled=true</c>).
/// </summary>
internal sealed class QuartzJobScheduler : IJobScheduler
{
    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task EnqueueAsync<T>(T job, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.x): Quartz.NET integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task ScheduleAsync<T>(T job, DateTimeOffset runAt, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.x): Quartz.NET integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task ScheduleRecurringAsync<T>(string name, T job, string cronExpression, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.x): Quartz.NET integration");
}

/// <summary>
/// TODO(Phase 5.x): MassTransit scheduled job scheduler — uses MT message scheduling.
/// TODO(M-O4): stub — register conditionally only when adapter implemented
///   (gate on <c>SaasBuilder:Schedulers:MassTransit:Enabled=true</c>).
/// </summary>
internal sealed class MassTransitScheduledJobScheduler : IJobScheduler
{
    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task EnqueueAsync<T>(T job, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.x): MassTransit scheduled job integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task ScheduleAsync<T>(T job, DateTimeOffset runAt, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.x): MassTransit scheduled job integration");

    // TODO(M-O4): stub — register conditionally only when adapter implemented
    public Task ScheduleRecurringAsync<T>(string name, T job, string cronExpression, CancellationToken ct = default) where T : IJob
        => throw new NotImplementedException("TODO(Phase 5.x): MassTransit scheduled job integration");
}
