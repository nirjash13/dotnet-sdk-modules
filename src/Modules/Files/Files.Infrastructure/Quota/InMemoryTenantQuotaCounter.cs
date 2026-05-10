using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Files.Application.Abstractions;
using Files.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Files.Infrastructure.Quota;

/// <summary>
/// In-memory tenant quota counter.
/// Suitable for development and single-instance deployments.
/// Production deployments should replace with a DB-backed counter (EF Core or Redis).
/// TODO(Phase 5.2): implement EfCoreTenantQuotaCounter backed by a PostgreSQL table.
/// </summary>
internal sealed class InMemoryTenantQuotaCounter(IOptions<FilesOptions> options) : ITenantQuotaCounter
{
    private readonly FilesOptions _opts = options.Value;
    private readonly ConcurrentDictionary<Guid, long> _usage = new();

    /// <inheritdoc />
    public Task<long> GetUsedBytesAsync(Guid tenantId, CancellationToken ct = default)
    {
        long used = _usage.GetValueOrDefault(tenantId, 0L);
        return Task.FromResult(used);
    }

    /// <inheritdoc />
    public Task IncrementAsync(Guid tenantId, long bytesDelta, CancellationToken ct = default)
    {
        _usage.AddOrUpdate(tenantId, bytesDelta, (_, current) => Math.Max(0, current + bytesDelta));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<long?> GetHardLimitAsync(Guid tenantId, CancellationToken ct = default)
    {
        long? limit = _opts.DefaultHardQuotaBytes > 0 ? _opts.DefaultHardQuotaBytes : null;
        return Task.FromResult(limit);
    }

    /// <inheritdoc />
    public Task<long?> GetSoftLimitAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_opts.DefaultHardQuotaBytes <= 0)
        {
            return Task.FromResult<long?>(null);
        }

        long soft = (long)(_opts.DefaultHardQuotaBytes * _opts.SoftQuotaFraction);
        return Task.FromResult<long?>(soft);
    }
}
