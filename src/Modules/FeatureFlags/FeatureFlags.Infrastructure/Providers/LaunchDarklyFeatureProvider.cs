using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Application.Abstractions;
using FeatureFlags.Contracts;

namespace FeatureFlags.Infrastructure.Providers;

/// <summary>
/// LaunchDarkly feature provider stub.
/// TODO(Phase 4.9): Install LaunchDarkly.ServerSdk NuGet package and implement.
/// </summary>
public sealed class LaunchDarklyFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public string Name => "launchdarkly";

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<bool>> ResolveBooleanAsync(
        string flagKey, bool defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): LaunchDarkly provider — install LaunchDarkly.ServerSdk.");

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<int>> ResolveIntegerAsync(
        string flagKey, int defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): LaunchDarkly provider.");

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<string>> ResolveStringAsync(
        string flagKey, string defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): LaunchDarkly provider.");
}
