using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Application.Abstractions;
using FeatureFlags.Contracts;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace FeatureFlags.Application;

/// <summary>
/// Default <see cref="IFeatureClient"/> implementation.
/// Delegates to <see cref="IFeatureProvider"/> after auto-populating <see cref="EvaluationContext"/>
/// from the current tenant context.
/// </summary>
public sealed class FeatureClient(
    IFeatureProvider provider,
    ITenantContextAccessor tenantAccessor,
    ILogger<FeatureClient> logger)
    : IFeatureClient
{
    /// <inheritdoc />
    public async ValueTask<bool> GetBooleanValueAsync(
        string flagKey,
        bool defaultValue,
        EvaluationContext? ctx = null,
        CancellationToken ct = default)
    {
        FlagEvaluationDetails<bool> details = await provider
            .ResolveBooleanAsync(flagKey, defaultValue, EnrichContext(ctx), ct)
            .ConfigureAwait(false);

        LogEvaluation(flagKey, details.Reason, details.ErrorMessage);
        return details.Value;
    }

    /// <inheritdoc />
    public async ValueTask<int> GetIntegerValueAsync(
        string flagKey,
        int defaultValue,
        EvaluationContext? ctx = null,
        CancellationToken ct = default)
    {
        FlagEvaluationDetails<int> details = await provider
            .ResolveIntegerAsync(flagKey, defaultValue, EnrichContext(ctx), ct)
            .ConfigureAwait(false);

        LogEvaluation(flagKey, details.Reason, details.ErrorMessage);
        return details.Value;
    }

    /// <inheritdoc />
    public async ValueTask<string> GetStringValueAsync(
        string flagKey,
        string defaultValue,
        EvaluationContext? ctx = null,
        CancellationToken ct = default)
    {
        FlagEvaluationDetails<string> details = await provider
            .ResolveStringAsync(flagKey, defaultValue, EnrichContext(ctx), ct)
            .ConfigureAwait(false);

        LogEvaluation(flagKey, details.Reason, details.ErrorMessage);
        return details.Value;
    }

    /// <summary>
    /// Merges the caller-supplied context with tenant identity from <see cref="ITenantContextAccessor"/>.
    /// Tenant identity always wins over caller-supplied values to prevent spoofing.
    /// </summary>
    private EvaluationContext EnrichContext(EvaluationContext? callerCtx)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return callerCtx ?? EvaluationContext.Empty;
        }

        return (callerCtx ?? EvaluationContext.Empty) with
        {
            TenantId = tenant.TenantId,
            UserId = tenant.UserId,
        };
    }

    private void LogEvaluation(string flagKey, EvaluationReason reason, string? errorMessage)
    {
        if (reason == EvaluationReason.Error)
        {
            logger.LogWarning(
                "Flag '{FlagKey}' evaluation error: {Error}", flagKey, errorMessage);
        }
    }
}
