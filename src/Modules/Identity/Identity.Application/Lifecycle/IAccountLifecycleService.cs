using System;
using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Lifecycle;

/// <summary>
/// Account lifecycle management: deletion grace period, restore, and hard-delete.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): account deletion with 30-day grace period.
/// - Deletion request: marks account for deletion, schedules hard-delete job.
/// - Restore within grace period: cancels scheduled hard-delete.
/// - Hard-delete: purges all user data after grace period.
/// </remarks>
public interface IAccountLifecycleService
{
    /// <summary>Initiates a soft-delete with the configured grace period.</summary>
    Task RequestDeletionAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Restores a soft-deleted account within the grace period.</summary>
    Task RestoreAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes all data for a user. Called by the grace-period cron job.</summary>
    Task HardDeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
