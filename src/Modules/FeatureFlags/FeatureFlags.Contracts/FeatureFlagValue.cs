namespace FeatureFlags.Contracts;

/// <summary>
/// Typed wrapper for a resolved feature flag value.
/// </summary>
/// <typeparam name="T">The value type (bool, int, string).</typeparam>
public sealed record FeatureFlagValue<T>(T value, string flagKey)
{
    /// <summary>Gets the resolved flag value.</summary>
    public T Value { get; init; } = value;

    /// <summary>Gets the flag key that was evaluated.</summary>
    public string FlagKey { get; init; } = flagKey;
}
