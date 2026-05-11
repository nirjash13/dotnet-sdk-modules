using System;

namespace Identity.Application.Lifecycle;

/// <summary>Command: initiates soft-deletion of the requesting user's account.</summary>
/// <param name="UserId">The user requesting deletion.</param>
/// <param name="RestoreBaseUrl">Base URL for the restore link (e.g., "https://app.example.com/account/restore").</param>
public sealed record RequestAccountDeletionCommand(Guid UserId, string RestoreBaseUrl);
