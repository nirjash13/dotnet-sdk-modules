using System;
using Billing.Domain.Exceptions;

namespace Billing.Domain.Entities;

/// <summary>
/// An entitlement that is granted to all tenants on an <see cref="Edition"/>.
/// Stored as a value object within an edition.
/// </summary>
public sealed class EntitlementGrant
{
    private EntitlementGrant()
    {
    }

    /// <summary>Gets the machine-readable entitlement key (e.g., "advanced_reporting", "max_seats").</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the boolean entitlement value for flag-type entitlements.</summary>
    public bool? BoolValue { get; private set; }

    /// <summary>Gets the numeric limit for capacity-type entitlements (e.g., max seat count).</summary>
    public long? NumericLimit { get; private set; }

    /// <summary>Gets the string value for string-type entitlements.</summary>
    public string? StringValue { get; private set; }

    /// <summary>Gets the parent edition identifier (FK for EF Core).</summary>
    public Guid EditionId { get; private set; }

    /// <summary>Creates a boolean entitlement grant.</summary>
    public static EntitlementGrant ForBoolean(Guid editionId, string key, bool value)
    {
        ValidateKey(key);
        return new EntitlementGrant
        {
            EditionId = editionId,
            Key = key.Trim(),
            BoolValue = value,
        };
    }

    /// <summary>Creates a numeric limit entitlement grant.</summary>
    public static EntitlementGrant ForNumericLimit(Guid editionId, string key, long limit)
    {
        ValidateKey(key);
        if (limit < 0)
        {
            throw new BillingDomainException("Numeric limit must not be negative.");
        }

        return new EntitlementGrant
        {
            EditionId = editionId,
            Key = key.Trim(),
            NumericLimit = limit,
        };
    }

    /// <summary>Creates a string entitlement grant.</summary>
    public static EntitlementGrant ForString(Guid editionId, string key, string value)
    {
        ValidateKey(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BillingDomainException("String entitlement value must not be empty.");
        }

        return new EntitlementGrant
        {
            EditionId = editionId,
            Key = key.Trim(),
            StringValue = value,
        };
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BillingDomainException("Entitlement key must not be empty.");
        }
    }
}
