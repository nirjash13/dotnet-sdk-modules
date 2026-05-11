using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Application.Models;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Application.Handlers;

/// <summary>
/// Handles approval or denial of a pending high-sensitivity admin action.
/// </summary>
public sealed class ApproveAdminActionHandler(IPendingAdminActionStore store)
{
    /// <summary>Approves a pending action, allowing it to execute.</summary>
    public async Task<Result<PendingAdminAction>> ApproveAsync(
        Guid actionId,
        string approverId,
        CancellationToken ct = default)
    {
        PendingAdminAction? action = await store.FindAsync(actionId, ct).ConfigureAwait(false);

        if (action is null)
        {
            return Result<PendingAdminAction>.Failure($"Pending action '{actionId}' not found.");
        }

        if (action.Status != PendingActionStatus.Pending)
        {
            return Result<PendingAdminAction>.Failure(
                $"Action '{actionId}' is already {action.Status}.");
        }

        if (action.RequestorId == approverId)
        {
            return Result<PendingAdminAction>.Failure(
                "An admin cannot approve their own pending action.");
        }

        await store.ApproveAsync(actionId, approverId, ct).ConfigureAwait(false);

        action.Status = PendingActionStatus.Approved;
        action.ApproverId = approverId;
        action.ResolvedAt = DateTimeOffset.UtcNow;

        return Result<PendingAdminAction>.Success(action);
    }

    /// <summary>Denies a pending action.</summary>
    public async Task<Result> DenyAsync(
        Guid actionId,
        string approverId,
        string reason,
        CancellationToken ct = default)
    {
        PendingAdminAction? action = await store.FindAsync(actionId, ct).ConfigureAwait(false);

        if (action is null)
        {
            return Result.Failure($"Pending action '{actionId}' not found.");
        }

        if (action.Status != PendingActionStatus.Pending)
        {
            return Result.Failure($"Action '{actionId}' is already {action.Status}.");
        }

        await store.DenyAsync(actionId, approverId, reason, ct).ConfigureAwait(false);

        return Result.Success();
    }
}
