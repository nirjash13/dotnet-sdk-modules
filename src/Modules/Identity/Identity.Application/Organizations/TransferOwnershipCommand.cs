using System;

namespace Identity.Application.Organizations;

/// <summary>
/// Command to initiate an ownership transfer.
/// Generates a confirmation token and sends an email to the current owner.
/// </summary>
/// <param name="OrganizationId">The organization whose ownership is being transferred.</param>
/// <param name="NewOwnerUserId">The user who will become the new owner.</param>
/// <param name="CurrentOwnerUserId">The current owner requesting the transfer.</param>
/// <param name="OwnerRoleId">The id of the Owner role.</param>
public sealed record TransferOwnershipCommand(
    Guid OrganizationId,
    Guid NewOwnerUserId,
    Guid CurrentOwnerUserId,
    Guid OwnerRoleId);

/// <summary>Result returned when ownership transfer is initiated.</summary>
/// <param name="ConfirmationToken">
/// The raw confirmation token to send via email.
/// TODO(Phase 2 — implementation): email dispatch via INotificationDispatcher.
/// </param>
public sealed record TransferOwnershipResult(string ConfirmationToken);
