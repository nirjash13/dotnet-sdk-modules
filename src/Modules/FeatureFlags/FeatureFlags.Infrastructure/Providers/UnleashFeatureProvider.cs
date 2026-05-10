using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Application.Abstractions;
using FeatureFlags.Contracts;

namespace FeatureFlags.Infrastructure.Providers;

/// <summary>
/// Unleash feature provider stub.
/// TODO(Phase 4.9): Install Unleash.Client NuGet package and implement.
/// </summary>
public sealed class UnleashFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public string Name => "unleash";

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<bool>> ResolveBooleanAsync(
        string flagKey, bool defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): Unleash provider — install Unleash.Client.");

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<int>> ResolveIntegerAsync(
        string flagKey, int defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): Unleash provider.");

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<string>> ResolveStringAsync(
        string flagKey, string defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): Unleash provider.");
}
