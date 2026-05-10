using System;
using System.Threading;
using System.Threading.Tasks;

namespace Files.Application.Abstractions;

/// <summary>
/// Tracks cumulative blob storage usage per tenant and enforces quotas.
/// </summary>
public interface ITenantQuotaCounter
{
    /// <summary>Returns total bytes used by the specified tenant.</summary>
    Task<long> GetUsedBytesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Increments the usage counter for <paramref name="tenantId"/> by <paramref name="bytesDelta"/>.
    /// Pass a negative value to decrement (on delete).
    /// </summary>
    Task IncrementAsync(Guid tenantId, long bytesDelta, CancellationToken ct = default);

    /// <summary>Returns the hard byte limit for the given tenant, or null if unlimited.</summary>
    Task<long?> GetHardLimitAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Returns the soft warning byte threshold (default 80% of hard limit), or null if not set.</summary>
    Task<long?> GetSoftLimitAsync(Guid tenantId, CancellationToken ct = default);
}
