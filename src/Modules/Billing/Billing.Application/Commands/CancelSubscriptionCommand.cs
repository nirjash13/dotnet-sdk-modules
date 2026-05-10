using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Commands;

/// <summary>
/// Cancels a tenant's active subscription.
/// </summary>
public sealed class CancelSubscriptionCommand : ICommand<Result<bool>>
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public CancelSubscriptionCommand()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public CancelSubscriptionCommand(Guid tenantId, bool atPeriodEnd)
    {
        TenantId = tenantId;
        AtPeriodEnd = atPeriodEnd;
    }

    /// <summary>Gets the tenant whose subscription to cancel.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets a value indicating whether to cancel at the end of the current billing period or immediately.</summary>
    public bool AtPeriodEnd { get; set; }
}
