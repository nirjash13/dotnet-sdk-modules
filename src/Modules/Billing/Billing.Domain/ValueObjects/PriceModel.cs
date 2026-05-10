namespace Billing.Domain.ValueObjects;

/// <summary>
/// Defines the pricing model for a <see cref="Entities.Price"/> object.
/// </summary>
public enum PriceModel
{
    /// <summary>A single charge, paid once.</summary>
    OneTime = 0,

    /// <summary>A recurring subscription charge (monthly, annual, etc.).</summary>
    Recurring = 1,

    /// <summary>Tiered pricing with per-unit rates that change at volume thresholds.</summary>
    Tiered = 2,

    /// <summary>Graduated pricing where each tier is charged at its own rate.</summary>
    Graduated = 3,

    /// <summary>Volume pricing: a single rate applies to the entire quantity based on which tier the total falls into.</summary>
    Volume = 4,
}
