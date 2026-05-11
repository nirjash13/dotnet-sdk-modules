using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Application.Handlers;

/// <summary>
/// Command to write a tenant-level feature-flag override.
/// </summary>
public sealed class OverrideFeatureFlagCommand
{
    /// <summary>Gets or sets the target tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the flag key.</summary>
    public string FlagKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the override boolean value.</summary>
    public bool Value { get; set; }

    /// <summary>Gets or sets the reason for the override.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Writes a tenant-level feature-flag override via the abstracted store.
/// The concrete implementation lives in Admin.Infrastructure (layer boundary respected).
/// </summary>
public sealed class OverrideFeatureFlagHandler(IFeatureFlagOverrideStore overrideStore)
{
    /// <summary>Persists the feature-flag override for the given tenant.</summary>
    public async Task<Result> HandleAsync(
        Guid tenantId,
        string flagKey,
        bool value,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return Result.Failure("Flag key is required.");
        }

        return await overrideStore.SetOverrideAsync(tenantId, flagKey, value, ct).ConfigureAwait(false);
    }
}
