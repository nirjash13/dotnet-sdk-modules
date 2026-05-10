using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Commands;

/// <summary>Pauses an active subscription.</summary>
public sealed class PauseSubscriptionCommand : ICommand<Result<bool>>
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public PauseSubscriptionCommand()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public PauseSubscriptionCommand(Guid tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>Gets the tenant whose subscription to pause.</summary>
    public Guid TenantId { get; set; }
}
