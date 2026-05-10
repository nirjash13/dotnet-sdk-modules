using System;

namespace Jobs.Infrastructure.Models;

/// <summary>Internal representation of a job sitting in the in-process queue.</summary>
internal sealed record QueuedJob(
    string JobTypeName,
    string PayloadJson,
    Guid TenantId,
    Guid? UserId,
    string? CorrelationId,
    DateTimeOffset EnqueuedAt,
    DateTimeOffset? RunAt);
