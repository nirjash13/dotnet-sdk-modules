using System;

namespace Jobs.Application.Models;

/// <summary>Retry policy for a failed job execution.</summary>
/// <param name="MaxAttempts">Maximum total attempts (including the first).</param>
/// <param name="InitialDelay">Delay before the first retry.</param>
/// <param name="BackoffMultiplier">Multiplier applied to each successive delay.</param>
public record RetryPolicy(
    int MaxAttempts = 3,
    TimeSpan InitialDelay = default,
    double BackoffMultiplier = 2.0)
{
    /// <summary>Default retry policy — 3 attempts, 5-second initial delay, 2x backoff.</summary>
    public static readonly RetryPolicy Default = new(
        MaxAttempts: 3,
        InitialDelay: TimeSpan.FromSeconds(5),
        BackoffMultiplier: 2.0);

    /// <summary>No retries — fail fast.</summary>
    public static readonly RetryPolicy NoRetry = new(MaxAttempts: 1);

    /// <summary>Computes the delay before the Nth retry (1-based).</summary>
    public TimeSpan DelayForAttempt(int attemptNumber)
    {
        double multiplier = Math.Pow(BackoffMultiplier, attemptNumber - 1);
        return TimeSpan.FromTicks((long)(InitialDelay.Ticks * multiplier));
    }
}
