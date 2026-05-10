namespace Entitlements.Domain;

/// <summary>
/// Discriminator for the value type of an <see cref="EntitlementGrant"/>.
/// </summary>
public enum EntitlementType
{
    /// <summary>Feature flag entitlement (enabled/disabled).</summary>
    Boolean = 0,

    /// <summary>Numeric capacity entitlement (e.g., max seat count, API call limit).</summary>
    Numeric = 1,

    /// <summary>String value entitlement (e.g., compute tier name).</summary>
    String = 2,
}
