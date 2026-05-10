using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Contracts;

namespace FeatureFlags.Application.Abstractions;

/// <summary>
/// High-level client for evaluating feature flags.
/// Automatically populates <see cref="EvaluationContext"/> from the current tenant context.
/// Mirrors the OpenFeature spec's <c>Client</c> API.
///
/// TODO(Phase 4): When OpenFeature .NET SDK ships GA stable v2.x, retire this interface
/// and use the official <c>OpenFeature.SDK.IFeatureClient</c>.
/// </summary>
public interface IFeatureClient
{
    /// <summary>
    /// Evaluates a boolean flag. Returns <paramref name="defaultValue"/> when the flag is
    /// not defined, disabled, or an error occurs.
    /// </summary>
    ValueTask<bool> GetBooleanValueAsync(
        string flagKey,
        bool defaultValue,
        EvaluationContext? ctx = null,
        CancellationToken ct = default);

    /// <summary>Evaluates an integer flag.</summary>
    ValueTask<int> GetIntegerValueAsync(
        string flagKey,
        int defaultValue,
        EvaluationContext? ctx = null,
        CancellationToken ct = default);

    /// <summary>Evaluates a string flag.</summary>
    ValueTask<string> GetStringValueAsync(
        string flagKey,
        string defaultValue,
        EvaluationContext? ctx = null,
        CancellationToken ct = default);
}
