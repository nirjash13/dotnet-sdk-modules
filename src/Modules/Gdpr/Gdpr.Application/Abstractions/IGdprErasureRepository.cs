using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Contracts;

namespace Gdpr.Application.Abstractions;

/// <summary>Repository for erasure request lifecycle.</summary>
public interface IGdprErasureRepository
{
    /// <summary>Creates a new pending erasure request and returns its DTO.</summary>
    Task<ErasureRequestDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset graceEndsAt,
        CancellationToken ct = default);

    /// <summary>Cancels an erasure request. Returns false if not found or already completed.</summary>
    Task<bool> CancelAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all pending requests whose grace period has elapsed.</summary>
    Task<IReadOnlyList<ErasureRequestDto>> GetOverdueAsync(DateTimeOffset asOf, CancellationToken ct = default);

    /// <summary>Marks a request as completed.</summary>
    Task MarkCompletedAsync(Guid id, DateTimeOffset completedAt, CancellationToken ct = default);

    /// <summary>Returns a single erasure request, or null if not found.</summary>
    Task<ErasureRequestDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
