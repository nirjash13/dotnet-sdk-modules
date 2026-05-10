using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Commands;

/// <summary>Resumes a paused subscription.</summary>
public sealed class ResumeSubscriptionCommand : ICommand<Result<bool>>
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public ResumeSubscriptionCommand()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public ResumeSubscriptionCommand(Guid tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>Gets the tenant whose subscription to resume.</summary>
    public Guid TenantId { get; set; }
}
