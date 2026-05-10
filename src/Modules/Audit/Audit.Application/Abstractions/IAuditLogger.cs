using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Models;

namespace Audit.Application.Abstractions;

/// <summary>Records audit events to the append-only audit log.</summary>
public interface IAuditLogger
{
    /// <summary>
    /// Records <paramref name="evt"/> asynchronously.
    /// Never throws — on transient failure, implementations must log the failure and no-op.
    /// </summary>
    /// <param name="evt">The audit event to record.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordAsync(AuditEvent evt, CancellationToken ct = default);
}
