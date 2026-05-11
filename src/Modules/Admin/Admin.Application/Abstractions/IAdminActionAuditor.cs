using System;
using System.Threading;
using System.Threading.Tasks;

namespace Admin.Application.Abstractions;

/// <summary>
/// Records every admin action to the append-only admin audit log.
/// Implementations must never throw — log failures are non-fatal.
/// </summary>
public interface IAdminActionAuditor
{
    /// <summary>Records an admin action asynchronously.</summary>
    /// <param name="actorId">The system admin's sub claim.</param>
    /// <param name="action">The action name in dot-notation (e.g., "tenant.impersonate").</param>
    /// <param name="targetTenantId">The tenant this action targeted, if applicable.</param>
    /// <param name="payloadJson">Serialized (PII-redacted) request payload.</param>
    /// <param name="ipAddress">Client IP address.</param>
    /// <param name="userAgent">HTTP User-Agent header.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordAsync(
        string actorId,
        string action,
        Guid? targetTenantId,
        string? payloadJson,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);
}
