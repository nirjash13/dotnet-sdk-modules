using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gdpr.Application.Abstractions;

/// <summary>
/// Implemented by each module to contribute PII rows to a GDPR data export.
/// Each implementation must return only data belonging to the specified user and tenant.
/// </summary>
public interface IExportable
{
    /// <summary>Short name used as the CSV/JSON filename inside the zip (e.g. "identity_users").</summary>
    string TableName { get; }

    /// <summary>
    /// Returns rows of PII data for the specified user.
    /// Each row is a dictionary of column-name → value (JSON-serialisable).
    /// </summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExportAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken ct = default);
}
