using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Audit.Contracts;

namespace Audit.Application.Abstractions;

/// <summary>Read model for the audit event API.</summary>
public interface IAuditEventQuery
{
    /// <summary>Returns paginated audit events filtered by the specified criteria.</summary>
    Task<(int Total, IReadOnlyList<AuditEventDto> Items)> QueryAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? actor,
        string? resource,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
