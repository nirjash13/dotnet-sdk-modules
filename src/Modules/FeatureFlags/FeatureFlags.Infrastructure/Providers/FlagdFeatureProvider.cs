using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Application.Abstractions;
using FeatureFlags.Contracts;

namespace FeatureFlags.Infrastructure.Providers;

/// <summary>
/// Flagd feature provider stub (OpenFeature reference implementation).
/// TODO(Phase 4.9): Use flagd gRPC/HTTP API and implement.
/// </summary>
public sealed class FlagdFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public string Name => "flagd";

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<bool>> ResolveBooleanAsync(
        string flagKey, bool defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): Flagd provider.");

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<int>> ResolveIntegerAsync(
        string flagKey, int defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): Flagd provider.");

    /// <inheritdoc />
    public ValueTask<FlagEvaluationDetails<string>> ResolveStringAsync(
        string flagKey, string defaultValue, EvaluationContext? context, CancellationToken ct)
        => throw new System.NotImplementedException("TODO(Phase 4.9): Flagd provider.");
}
