using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="TransferOwnershipCommand"/>.
/// </summary>
/// <remarks>
/// TODO(Phase 2 — implementation): Full confirmation-token flow:
/// 1. Generate a short-lived confirmation token.
/// 2. Persist a pending transfer record.
/// 3. Dispatch a confirmation email to the current owner via INotificationDispatcher.
/// 4. On confirmation, swap Owner role assignments atomically.
/// The stub below returns NotImplementedException to make the unimplemented path loud.
/// </remarks>
public sealed class TransferOwnershipHandler
{
    /// <summary>Initializes a new instance of <see cref="TransferOwnershipHandler"/>.</summary>
    public TransferOwnershipHandler()
    {
    }

    /// <summary>
    /// Initiates an ownership transfer.
    /// </summary>
    public Task<Result<TransferOwnershipResult>> HandleAsync(
        TransferOwnershipCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // TODO(Phase 2 — implementation): ownership transfer with email-confirmation step.
        throw new NotImplementedException(
            "TODO(Phase 2 — implementation): TransferOwnership requires confirmation email dispatch. " +
            "Implement once INotificationDispatcher is available (Phase 5).");
    }
}
