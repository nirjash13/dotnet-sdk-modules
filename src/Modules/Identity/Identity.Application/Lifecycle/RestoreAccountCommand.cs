namespace Identity.Application.Lifecycle;

/// <summary>Command: restores a soft-deleted account using a single-use restore token.</summary>
/// <param name="RawToken">The raw restore token from the email link.</param>
public sealed record RestoreAccountCommand(string RawToken);
