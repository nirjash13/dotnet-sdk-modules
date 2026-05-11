using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Contracts;

namespace Admin.Application.Abstractions;

/// <summary>
/// Provides cross-module tenant directory queries for the admin control plane.
/// Implementations query Identity + Billing to build aggregated tenant views.
/// </summary>
public interface ITenantDirectoryService
{
    /// <summary>
    /// Returns a paginated tenant directory, optionally filtered by slug/name substring and status.
    /// </summary>
    Task<(int Total, System.Collections.Generic.IReadOnlyList<TenantSummaryDto> Items)> ListTenantsAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full inspector DTO for a single tenant, or <see langword="null"/> if not found.
    /// </summary>
    Task<TenantInspectorDto?> GetTenantInspectorAsync(Guid tenantId, CancellationToken ct = default);
}
