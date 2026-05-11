namespace Identity.Application.Auth;

/// <summary>
/// Configuration options for account lockout behavior.
/// Bound from <c>Identity:Lockout</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class LockoutOptions
{
    /// <summary>
    /// Gets or sets the number of consecutive failed login attempts that trigger a lockout.
    /// Default: 5.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the rolling window in minutes within which failed attempts are counted.
    /// Default: 15 minutes.
    /// </summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>
    /// Gets or sets the duration in minutes an account is locked after exceeding the threshold.
    /// Default: 30 minutes.
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 30;
}
