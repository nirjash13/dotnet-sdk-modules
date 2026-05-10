using System;
using System.Collections.Generic;

namespace FeatureFlags.Contracts;

/// <summary>
/// Context supplied to flag evaluation, matching the OpenFeature spec's EvaluationContext shape.
/// Auto-populated from <c>ITenantContext</c> by <c>FeatureClient</c> before calling <c>IFeatureProvider</c>.
///
/// TODO(Phase 4): When OpenFeature .NET SDK ships a GA stable v2.x, swap our EvaluationContext
/// for <c>OpenFeature.Model.EvaluationContext</c> and make <c>IFeatureClient</c> a thin adapter
/// over <c>OpenFeature.SDK.IFeatureClient</c>.
/// </summary>
public sealed record EvaluationContext
{
    /// <summary>Gets an empty evaluation context.</summary>
    public static readonly EvaluationContext Empty = new EvaluationContext();

    /// <summary>Gets the tenant identifier for this evaluation.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Gets the authenticated user identifier for this evaluation.</summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Gets custom attributes for targeting rule evaluation (e.g., plan, region, betaUser).
    /// Keys are case-insensitive.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates an evaluation context from raw values.</summary>
    public static EvaluationContext For(
        Guid tenantId,
        Guid? userId = null,
        Dictionary<string, string>? attributes = null)
        => new EvaluationContext
        {
            TenantId = tenantId,
            UserId = userId,
            Attributes = attributes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
}
