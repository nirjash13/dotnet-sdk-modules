using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Contracts;

namespace FeatureFlags.Application.Abstractions;

/// <summary>
/// Low-level flag evaluation provider — mirrors the OpenFeature spec's <c>FeatureProvider</c> interface.
/// Implementations: <c>DatabaseFeatureProvider</c> (default), plus stubs for LaunchDarkly, Unleash, etc.
///
/// TODO(Phase 4): When OpenFeature .NET SDK ships GA stable v2.x, replace this interface with
/// <c>OpenFeature.SDK.IFeatureProvider</c> and update <c>FeatureClient</c> accordingly.
/// </summary>
public interface IFeatureProvider
{
    /// <summary>Gets the human-readable provider name (e.g., "database", "launchdarkly").</summary>
    string Name { get; }

    /// <summary>Resolves a boolean flag value.</summary>
    ValueTask<FlagEvaluationDetails<bool>> ResolveBooleanAsync(
        string flagKey,
        bool defaultValue,
        EvaluationContext? context,
        CancellationToken ct = default);

    /// <summary>Resolves an integer flag value.</summary>
    ValueTask<FlagEvaluationDetails<int>> ResolveIntegerAsync(
        string flagKey,
        int defaultValue,
        EvaluationContext? context,
        CancellationToken ct = default);

    /// <summary>Resolves a string flag value.</summary>
    ValueTask<FlagEvaluationDetails<string>> ResolveStringAsync(
        string flagKey,
        string defaultValue,
        EvaluationContext? context,
        CancellationToken ct = default);
}
