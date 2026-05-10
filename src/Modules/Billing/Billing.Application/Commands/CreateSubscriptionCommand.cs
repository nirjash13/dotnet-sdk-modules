using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Commands;

/// <summary>
/// Creates a new subscription for a tenant after checkout session completion.
/// </summary>
public sealed class CreateSubscriptionCommand : ICommand<Result<Guid>>
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public CreateSubscriptionCommand()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public CreateSubscriptionCommand(
        Guid tenantId,
        Guid planId,
        string? providerSubscriptionId,
        DateTimeOffset? trialEndsAt)
    {
        TenantId = tenantId;
        PlanId = planId;
        ProviderSubscriptionId = providerSubscriptionId;
        TrialEndsAt = trialEndsAt;
    }

    /// <summary>Gets the tenant to subscribe.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets the plan being subscribed to.</summary>
    public Guid PlanId { get; set; }

    /// <summary>Gets the optional provider-side subscription identifier.</summary>
    public string? ProviderSubscriptionId { get; set; }

    /// <summary>Gets the optional trial end date.</summary>
    public DateTimeOffset? TrialEndsAt { get; set; }
}
