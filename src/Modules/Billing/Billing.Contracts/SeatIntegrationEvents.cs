using System;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Contracts;

/// <summary>
/// Published by the Identity module when a member is added to an organization.
/// Consumed by <c>SeatSyncService</c> in the Billing module to update the Stripe quantity.
/// </summary>
public sealed class MemberAddedIntegrationEvent : IIntegrationEvent
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public MemberAddedIntegrationEvent()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public MemberAddedIntegrationEvent(Guid tenantId, Guid memberId, int newSeatCount)
    {
        TenantId = tenantId;
        MemberId = memberId;
        NewSeatCount = newSeatCount;
    }

    /// <summary>Gets the tenant (organization) the member was added to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets the member user identifier.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Gets the new total seat count after the addition.</summary>
    public int NewSeatCount { get; set; }
}

/// <summary>
/// Published by the Identity module when a member is removed from an organization.
/// Consumed by <c>SeatSyncService</c> in the Billing module to update the Stripe quantity.
/// </summary>
public sealed class MemberRemovedIntegrationEvent : IIntegrationEvent
{
    /// <summary>Parameterless constructor required by transport serializers.</summary>
    public MemberRemovedIntegrationEvent()
    {
    }

    /// <summary>Initializes with required fields.</summary>
    public MemberRemovedIntegrationEvent(Guid tenantId, Guid memberId, int newSeatCount)
    {
        TenantId = tenantId;
        MemberId = memberId;
        NewSeatCount = newSeatCount;
    }

    /// <summary>Gets the tenant (organization) the member was removed from.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets the member user identifier.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Gets the new total seat count after the removal.</summary>
    public int NewSeatCount { get; set; }
}
