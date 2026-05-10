namespace FeatureFlags.Contracts;

/// <summary>
/// Detailed flag evaluation result, including the reason the value was chosen.
/// Mirrors the OpenFeature spec's <c>FlagEvaluationDetails&lt;T&gt;</c> structure.
/// </summary>
/// <typeparam name="T">The value type (bool, int, string).</typeparam>
public sealed record FlagEvaluationDetails<T>
{
    /// <summary>Gets the flag key that was evaluated.</summary>
    public required string FlagKey { get; init; }

    /// <summary>Gets the resolved flag value.</summary>
    public required T Value { get; init; }

    /// <summary>Gets the reason the value was returned.</summary>
    public required EvaluationReason Reason { get; init; }

    /// <summary>Gets an optional error message when <see cref="Reason"/> is <see cref="EvaluationReason.Error"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the variant key that matched (for targeting rules), if applicable.</summary>
    public string? Variant { get; init; }
}

/// <summary>
/// The reason a particular flag evaluation result was returned.
/// Mirrors the OpenFeature spec's <c>Reason</c> enum.
/// </summary>
public enum EvaluationReason
{
    /// <summary>The default value was returned because the flag is not defined.</summary>
    Default = 0,

    /// <summary>A targeting rule matched the evaluation context.</summary>
    TargetingMatch = 1,

    /// <summary>The flag is explicitly disabled (kill-switch).</summary>
    Disabled = 2,

    /// <summary>A static override (tenant or global) returned the value.</summary>
    Static = 3,

    /// <summary>An error occurred during evaluation; the default value was returned.</summary>
    Error = 4,
}
