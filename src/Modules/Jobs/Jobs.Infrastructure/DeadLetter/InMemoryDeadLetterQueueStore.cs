using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Jobs.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Jobs.Infrastructure.DeadLetter;

/// <summary>
/// In-memory DLQ store for development / Phase 5 scaffold.
/// TODO(Phase 5.3): replace with EF Core-backed store that persists DLQ entries across restarts.
/// </summary>
internal sealed class InMemoryDeadLetterQueueStore(ILogger<InMemoryDeadLetterQueueStore> logger)
    : IDeadLetterQueueStore
{
    private readonly ConcurrentBag<DeadLetterEntry> _entries = new();

    /// <inheritdoc />
    public Task AddAsync(
        string jobType,
        string payloadJson,
        Guid tenantId,
        string error,
        CancellationToken ct = default)
    {
        _entries.Add(new DeadLetterEntry(jobType, payloadJson, tenantId, error, DateTimeOffset.UtcNow));
        logger.LogError(
            "Jobs.DLQ: job type='{Type}' tenant={TenantId} moved to dead-letter queue. Error: {Error}",
            jobType, tenantId, error);
        return Task.CompletedTask;
    }

    private sealed record DeadLetterEntry(
        string JobType,
        string PayloadJson,
        Guid TenantId,
        string Error,
        DateTimeOffset DeadLetteredAt);
}
