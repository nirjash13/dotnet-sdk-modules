namespace Billing.Domain.ValueObjects;

/// <summary>
/// Defines the billing interval for a recurring <see cref="Entities.Price"/>.
/// </summary>
public enum BillingInterval
{
    /// <summary>Billed every day.</summary>
    Daily = 0,

    /// <summary>Billed every week.</summary>
    Weekly = 1,

    /// <summary>Billed every month.</summary>
    Monthly = 2,

    /// <summary>Billed every year.</summary>
    Annual = 3,
}
