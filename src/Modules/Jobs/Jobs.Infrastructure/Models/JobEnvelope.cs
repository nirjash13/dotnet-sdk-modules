using System;

namespace Jobs.Infrastructure.Models;

/// <summary>
/// Wraps a job payload for queuing. Carries tenant context so it can be
/// restored on the dequeue thread before handler invocation.
/// </summary>
/// <typeparam name="T">The job payload type.</typeparam>
internal sealed record JobEnvelope<T>(
    T Job,
    Guid TenantId,
    Guid? UserId,
    string? CorrelationId,
    DateTimeOffset EnqueuedAt,
    DateTimeOffset? RunAt);
