namespace Identity.Application.Lifecycle;

/// <summary>
/// Command: finds all users whose deletion grace period has expired and hard-deletes them.
/// Typically invoked by a Hangfire recurring job at 03:00 UTC daily.
/// </summary>
public sealed record HardDeleteExpiredAccountsCommand;
