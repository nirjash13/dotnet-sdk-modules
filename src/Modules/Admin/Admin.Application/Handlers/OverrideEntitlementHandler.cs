using System;
using System.Threading;
using System.Threading.Tasks;
using Entitlements.Application.Abstractions;
using Entitlements.Domain;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Application.Handlers;

/// <summary>
/// Writes a tenant-level entitlement override via the Entitlements module's repository.
/// </summary>
public sealed class OverrideEntitlementHandler(IEntitlementRepository entitlementRepository)
{
    /// <summary>
    /// Persists the entitlement override for the given tenant.
    /// Value is parsed as: bool first, then long, then treated as bool-false (unrecognised type = disabled).
    /// </summary>
    public async Task<Result> HandleAsync(
        Guid tenantId,
        string key,
        string value,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure("Entitlement key is required.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure("Entitlement value is required.");
        }

        // Parse the value: try bool first, then long.
        EntitlementGrant grant;
        if (bool.TryParse(value, out bool boolVal))
        {
            grant = EntitlementGrant.ForTenantOverrideBoolean(tenantId, key, boolVal);
        }
        else if (long.TryParse(value, out long longVal))
        {
            grant = EntitlementGrant.ForTenantOverrideNumeric(tenantId, key, longVal);
        }
        else
        {
            // Non-boolean, non-numeric override: treat as boolean=false (disable the entitlement).
            // TODO(Phase 6.x): extend EntitlementGrant with a string factory when needed.
            return Result.Failure("Entitlement value must be a boolean (true/false) or a numeric limit.");
        }

        await entitlementRepository.AddOverrideAsync(grant, ct).ConfigureAwait(false);

        return Result.Success();
    }
}
