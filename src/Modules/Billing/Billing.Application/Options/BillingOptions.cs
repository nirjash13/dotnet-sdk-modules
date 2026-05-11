namespace Billing.Application.Options;

/// <summary>
/// Strongly-typed options for the Billing module.
/// Bound from configuration section <c>Billing</c>.
/// </summary>
public sealed class BillingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Billing";

    /// <summary>
    /// Gets or sets the dunning grace period in days between a terminal payment failure
    /// and actual tenant suspension. Default is 7.
    /// Set to 0 to suspend immediately on terminal failure (no grace period).
    /// Bound from <c>Billing:Dunning:GraceDays</c>.
    /// </summary>
    public int DunningGraceDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets whether to suspend the tenant when a terminal payment failure occurs.
    /// Default is <see langword="true"/>.
    /// Bound from <c>Billing:Dunning:SuspendOnTerminalFailure</c>.
    /// </summary>
    public bool SuspendOnTerminalPaymentFailure { get; set; } = true;
}
