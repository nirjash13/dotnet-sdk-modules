using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using FeatureFlags.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IFeatureFlagOverrideStore"/>.
/// Writes tenant-level flag overrides directly to the FeatureFlags bounded context.
/// </summary>
public sealed class FeatureFlagOverrideStore(FeatureFlagsDbContext dbContext) : IFeatureFlagOverrideStore
{
    /// <inheritdoc />
    public async Task<Result> SetOverrideAsync(
        Guid tenantId,
        string flagKey,
        bool value,
        CancellationToken ct = default)
    {
        FeatureFlag? flag = await dbContext.FeatureFlags
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == flagKey, ct)
            .ConfigureAwait(false);

        if (flag is null)
        {
            return Result.Failure($"Feature flag '{flagKey}' not found.");
        }

        TenantFlagOverride? existing = await dbContext.TenantFlagOverrides
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.FeatureFlagId == flag.Id, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            dbContext.TenantFlagOverrides.Add(TenantFlagOverride.Create(flag.Id, tenantId, value));
        }
        else
        {
            // Remove + add is safe here because the unique index is on (TenantId, FeatureFlagId).
            dbContext.TenantFlagOverrides.Remove(existing);
            dbContext.TenantFlagOverrides.Add(TenantFlagOverride.Create(flag.Id, tenantId, value));
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }
}
