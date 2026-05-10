using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Application.Abstractions;
using FeatureFlags.Contracts;
using FeatureFlags.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeatureFlags.Infrastructure.Providers;

/// <summary>
/// Default DB-backed feature flag provider.
/// Evaluation order (highest to lowest priority):
///   1. Kill-switch: flag.IsEnabled == false → return defaultValue with reason Disabled.
///   2. Tenant override: explicit per-tenant value → return with reason Static.
///   3. Targeting rules: first matching attribute-value rule → return with reason TargetingMatch.
///   4. Percentage rollout: deterministic hash(tenantId + flagKey) % 100 → in-bucket returns true.
///   5. Default value → return with reason Default.
/// </summary>
public sealed class DatabaseFeatureProvider(
    FeatureFlagsDbContext db,
    ILogger<DatabaseFeatureProvider> logger)
    : IFeatureProvider
{
    /// <inheritdoc />
    public string Name => "database";

    /// <inheritdoc />
    public async ValueTask<FlagEvaluationDetails<bool>> ResolveBooleanAsync(
        string flagKey,
        bool defaultValue,
        EvaluationContext? context,
        CancellationToken ct)
    {
        FeatureFlag? flag = await db.FeatureFlags
            .AsNoTracking()
            .Include(f => f.TargetingRules)
            .Include(f => f.TenantOverrides)
            .FirstOrDefaultAsync(f => f.Key == flagKey, ct)
            .ConfigureAwait(false);

        if (flag is null)
        {
            logger.LogDebug("Flag '{FlagKey}' not found — returning default ({Default}).", flagKey, defaultValue);
            return Details(flagKey, defaultValue, EvaluationReason.Default);
        }

        // 1. Kill-switch.
        if (!flag.IsEnabled)
        {
            return Details(flagKey, defaultValue, EvaluationReason.Disabled);
        }

        // 2. Tenant override.
        if (context?.TenantId is Guid tenantId)
        {
            TenantFlagOverride? overrideEntry = flag.TenantOverrides
                .FirstOrDefault(o => o.TenantId == tenantId);

            if (overrideEntry is not null)
            {
                return Details(flagKey, overrideEntry.Value, EvaluationReason.Static);
            }

            // 3. Targeting rules.
            if (context.Attributes is { Count: > 0 })
            {
                foreach (TargetingRule rule in flag.TargetingRules)
                {
                    if (context.Attributes.TryGetValue(rule.AttributeKey, out string? attrValue) &&
                        string.Equals(attrValue, rule.AttributeValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return Details(flagKey, rule.TargetValue, EvaluationReason.TargetingMatch, rule.AttributeKey);
                    }
                }
            }

            // 4. Percentage rollout — deterministic hash so same tenantId always gets same result.
            if (flag.RolloutPercentage > 0)
            {
                int bucket = ComputeBucket(tenantId, flagKey);
                if (bucket < flag.RolloutPercentage)
                {
                    return Details(flagKey, true, EvaluationReason.TargetingMatch);
                }
            }
        }

        // 5. Default value.
        return Details(flagKey, flag.DefaultBoolValue || defaultValue, EvaluationReason.Default);
    }

    /// <inheritdoc />
    public async ValueTask<FlagEvaluationDetails<int>> ResolveIntegerAsync(
        string flagKey,
        int defaultValue,
        EvaluationContext? context,
        CancellationToken ct)
    {
        // Integer flag support requires storing int values in the DB.
        // TODO(Phase 4.9): Extend FeatureFlag entity with IntValue column for integer flags.
        logger.LogWarning("Integer flag '{FlagKey}' evaluation not fully implemented — returning default.", flagKey);
        await Task.CompletedTask.ConfigureAwait(false);
        return new FlagEvaluationDetails<int>
        {
            FlagKey = flagKey,
            Value = defaultValue,
            Reason = EvaluationReason.Default,
        };
    }

    /// <inheritdoc />
    public async ValueTask<FlagEvaluationDetails<string>> ResolveStringAsync(
        string flagKey,
        string defaultValue,
        EvaluationContext? context,
        CancellationToken ct)
    {
        // String flag support requires storing string values in the DB.
        // TODO(Phase 4.9): Extend FeatureFlag entity with StringValue column for string flags.
        logger.LogWarning("String flag '{FlagKey}' evaluation not fully implemented — returning default.", flagKey);
        await Task.CompletedTask.ConfigureAwait(false);
        return new FlagEvaluationDetails<string>
        {
            FlagKey = flagKey,
            Value = defaultValue,
            Reason = EvaluationReason.Default,
        };
    }

    /// <summary>
    /// Computes a deterministic bucket value [0, 99] for a (tenantId, flagKey) pair.
    /// Same inputs always produce the same bucket — ensures consistent percentage rollout.
    /// Algorithm: SHA-256(tenantId + ":" + flagKey) → first 4 bytes as uint → modulo 100.
    /// </summary>
    internal static int ComputeBucket(Guid tenantId, string flagKey)
    {
        string input = $"{tenantId:N}:{flagKey}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        uint hashInt = BitConverter.ToUInt32(hash, 0);
        return (int)(hashInt % 100);
    }

    private static FlagEvaluationDetails<bool> Details(
        string flagKey,
        bool value,
        EvaluationReason reason,
        string? variant = null)
        => new FlagEvaluationDetails<bool>
        {
            FlagKey = flagKey,
            Value = value,
            Reason = reason,
            Variant = variant,
        };
}
