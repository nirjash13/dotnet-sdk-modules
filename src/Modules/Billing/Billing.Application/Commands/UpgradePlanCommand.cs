using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Commands;

/// <summary>
/// Upgrades a tenant's subscription to a new plan.
/// </summary>
public sealed class UpgradePlanCommand : ICommand<Result<bool>>
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public UpgradePlanCommand()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public UpgradePlanCommand(Guid tenantId, Guid newPlanId, string newPriceId)
    {
        TenantId = tenantId;
        NewPlanId = newPlanId;
        NewPriceId = newPriceId;
    }

    /// <summary>Gets the tenant performing the upgrade.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets the new plan identifier.</summary>
    public Guid NewPlanId { get; set; }

    /// <summary>Gets the new provider price identifier.</summary>
    public string NewPriceId { get; set; } = string.Empty;
}
