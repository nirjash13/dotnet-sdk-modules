using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gdpr.Application.Abstractions;

/// <summary>
/// Implemented by each module to anonymize PII for a specific user during erasure.
/// Anonymization overwrites PII columns with "[REDACTED]" and stores the SHA-256 hash
/// of the original value for audit purposes. Data is NOT deleted.
/// </summary>
public interface IErasureHandler
{
    /// <summary>
    /// Anonymizes all PII belonging to <paramref name="userId"/> within <paramref name="tenantId"/>.
    /// Must be idempotent — safe to call multiple times.
    /// </summary>
    Task AnonymizeAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
