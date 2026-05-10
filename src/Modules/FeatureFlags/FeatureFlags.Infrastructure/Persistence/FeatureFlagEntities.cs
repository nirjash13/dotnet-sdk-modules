using System;
using System.Collections.Generic;

namespace FeatureFlags.Infrastructure.Persistence;

/// <summary>
/// EF Core entity representing a feature flag definition.
/// </summary>
public sealed class FeatureFlag
{
    private FeatureFlag()
    {
    }

    /// <summary>Gets the flag identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the machine-readable flag key.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the flag description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets a value indicating whether the flag is active (kill-switch when false).</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Gets the default boolean value when no rule matches.</summary>
    public bool DefaultBoolValue { get; private set; }

    /// <summary>Gets the percentage of tenants that should receive the ON value (0-100).</summary>
    public int RolloutPercentage { get; private set; }

    /// <summary>Gets the UTC timestamp when the flag was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the associated targeting rules.</summary>
    public ICollection<TargetingRule> TargetingRules { get; private set; } = new List<TargetingRule>();

    /// <summary>Gets the tenant-level overrides for this flag.</summary>
    public ICollection<TenantFlagOverride> TenantOverrides { get; private set; } = new List<TenantFlagOverride>();

    /// <summary>Creates a new feature flag.</summary>
    public static FeatureFlag Create(string key, string? description, int rolloutPercentage = 0) =>
        new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = key.Trim(),
            Description = description?.Trim(),
            IsEnabled = true,
            DefaultBoolValue = false,
            RolloutPercentage = rolloutPercentage,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>Updates the rollout percentage.</summary>
    public void SetRolloutPercentage(int percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Rollout percentage must be 0-100.");
        }

        RolloutPercentage = percentage;
    }

    /// <summary>Disables the flag (kill-switch).</summary>
    public void Disable() => IsEnabled = false;

    /// <summary>Enables the flag.</summary>
    public void Enable() => IsEnabled = true;
}

/// <summary>
/// Targeting rule: when the specified context attribute equals the given value, apply the override.
/// </summary>
public sealed class TargetingRule
{
    private TargetingRule()
    {
    }

    /// <summary>Gets the rule identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the parent flag identifier.</summary>
    public Guid FeatureFlagId { get; private set; }

    /// <summary>Gets the context attribute key to match against (e.g., "plan", "region").</summary>
    public string AttributeKey { get; private set; } = string.Empty;

    /// <summary>Gets the required attribute value for this rule to match.</summary>
    public string AttributeValue { get; private set; } = string.Empty;

    /// <summary>Gets the boolean value to return when this rule matches.</summary>
    public bool TargetValue { get; private set; }

    /// <summary>Creates a targeting rule.</summary>
    public static TargetingRule Create(Guid flagId, string attributeKey, string attributeValue, bool targetValue) =>
        new TargetingRule
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flagId,
            AttributeKey = attributeKey.Trim(),
            AttributeValue = attributeValue.Trim(),
            TargetValue = targetValue,
        };
}

/// <summary>
/// Per-tenant override for a specific feature flag. Beats all other rules.
/// </summary>
public sealed class TenantFlagOverride
{
    private TenantFlagOverride()
    {
    }

    /// <summary>Gets the override identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the flag identifier.</summary>
    public Guid FeatureFlagId { get; private set; }

    /// <summary>Gets the tenant whose override this is.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the override boolean value.</summary>
    public bool Value { get; private set; }

    /// <summary>Creates a tenant override.</summary>
    public static TenantFlagOverride Create(Guid flagId, Guid tenantId, bool value) =>
        new TenantFlagOverride
        {
            Id = Guid.NewGuid(),
            FeatureFlagId = flagId,
            TenantId = tenantId,
            Value = value,
        };
}
