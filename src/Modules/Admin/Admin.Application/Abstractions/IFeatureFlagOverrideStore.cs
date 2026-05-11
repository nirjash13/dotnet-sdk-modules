using System;
using System.Threading;
using System.Threading.Tasks;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Application.Abstractions;

/// <summary>
/// Abstraction for persisting tenant-level feature-flag overrides.
/// Implemented in Admin.Infrastructure to avoid a direct dependency from Application
/// to FeatureFlags.Infrastructure (layer boundary enforcement).
/// </summary>
public interface IFeatureFlagOverrideStore
{
    /// <summary>
    /// Sets a boolean override for the specified tenant and flag key.
    /// Creates the record if it doesn't exist; replaces it if it does.
    /// Returns a failure result if the flag key is not found.
    /// </summary>
    Task<Result> SetOverrideAsync(
        Guid tenantId,
        string flagKey,
        bool value,
        CancellationToken ct = default);
}
