using System;

namespace Entitlements.Domain;

/// <summary>
/// Represents a single entitlement granted to a tenant, either from their edition
/// or as a sales-driven tenant-level override.
/// </summary>
public sealed class EntitlementGrant
{
    private EntitlementGrant()
    {
    }

    /// <summary>Gets the row identifier.</summary>
    public long Id { get; private set; }

    /// <summary>Gets the tenant identifier (null for edition-level grants).</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Gets the edition identifier (null for tenant-level override grants).</summary>
    public Guid? EditionId { get; private set; }

    /// <summary>Gets the machine-readable entitlement key.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the entitlement type.</summary>
    public EntitlementType Type { get; private set; }

    /// <summary>Gets the boolean value for <see cref="EntitlementType.Boolean"/> entitlements.</summary>
    public bool? BoolValue { get; private set; }

    /// <summary>Gets the numeric limit for <see cref="EntitlementType.Numeric"/> entitlements.</summary>
    public long? NumericLimit { get; private set; }

    /// <summary>Gets the string value for <see cref="EntitlementType.String"/> entitlements.</summary>
    public string? StringValue { get; private set; }

    /// <summary>Creates a boolean entitlement grant for an edition.</summary>
    public static EntitlementGrant ForEditionBoolean(Guid editionId, string key, bool value) =>
        new EntitlementGrant
        {
            EditionId = editionId,
            Key = key.Trim(),
            Type = EntitlementType.Boolean,
            BoolValue = value,
        };

    /// <summary>Creates a numeric limit entitlement grant for an edition.</summary>
    public static EntitlementGrant ForEditionNumeric(Guid editionId, string key, long limit) =>
        new EntitlementGrant
        {
            EditionId = editionId,
            Key = key.Trim(),
            Type = EntitlementType.Numeric,
            NumericLimit = limit,
        };

    /// <summary>Creates a tenant-level override grant (sales exception).</summary>
    public static EntitlementGrant ForTenantOverrideBoolean(Guid tenantId, string key, bool value) =>
        new EntitlementGrant
        {
            TenantId = tenantId,
            Key = key.Trim(),
            Type = EntitlementType.Boolean,
            BoolValue = value,
        };

    /// <summary>Creates a tenant-level numeric override grant (sales exception).</summary>
    public static EntitlementGrant ForTenantOverrideNumeric(Guid tenantId, string key, long limit) =>
        new EntitlementGrant
        {
            TenantId = tenantId,
            Key = key.Trim(),
            Type = EntitlementType.Numeric,
            NumericLimit = limit,
        };
}
