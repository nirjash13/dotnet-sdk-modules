using System;
using System.Threading;
using System.Threading.Tasks;
using Reporting.Application.Persistence;

namespace Reporting.Application.Abstractions;

/// <summary>
/// Application-layer abstraction for persisting and querying transaction projections.
/// Implemented by <c>ReportingDbContext</c> in the Infrastructure layer.
/// Keeps the Application layer free of EF Core dependencies.
/// </summary>
public interface IReportingDbContext
{
    /// <summary>
    /// Inserts a new projection row, ignoring conflicts on the
    /// <c>(TenantId, SourceMessageId)</c> unique index (idempotent upsert).
    /// </summary>
    /// <param name="projection">The projection row to insert.</param>
    /// <param name="ct">Cancellation token.</param>
    Task InsertIfNotExistsAsync(TransactionProjection projection, CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> if a projection row already exists for the
    /// given <paramref name="tenantId"/> and <paramref name="sourceMessageId"/>.
    /// </summary>
    Task<bool> ExistsAsync(Guid tenantId, Guid sourceMessageId, CancellationToken ct = default);
}
