using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Models;

namespace Admin.Application.Abstractions;

/// <summary>
/// Persists and retrieves pending admin actions awaiting a second approver.
/// </summary>
public interface IPendingAdminActionStore
{
    /// <summary>Creates a new pending action record and returns its identifier.</summary>
    Task<Guid> CreateAsync(PendingAdminAction action, CancellationToken ct = default);

    /// <summary>Returns the pending action by id, or <see langword="null"/> if not found or already resolved.</summary>
    Task<PendingAdminAction?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Marks a pending action as approved by the given approver.</summary>
    Task ApproveAsync(Guid id, string approverId, CancellationToken ct = default);

    /// <summary>Marks a pending action as denied.</summary>
    Task DenyAsync(Guid id, string approverId, string reason, CancellationToken ct = default);
}
