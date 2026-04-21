using System.Threading.Tasks;
using Identity.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Consumers;

/// <summary>
/// MassTransit consumer for the <see cref="TenantMembershipCreated"/> integration event.
/// Placeholder for Phase 2 — reaction logic (e.g., welcome email, audit log) is wired here.
/// </summary>
public sealed class TenantMembershipCreatedConsumer(
    ILogger<TenantMembershipCreatedConsumer> logger)
    : IConsumer<TenantMembershipCreated>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<TenantMembershipCreated> context)
    {
        TenantMembershipCreated message = context.Message;

        logger.LogInformation(
            "TenantMembership created: userId={UserId} tenantId={TenantId} membershipId={MembershipId} isPrimary={IsPrimary}",
            message.UserId,
            message.TenantId,
            message.MembershipId,
            message.IsPrimary);

        // TODO Phase 5: trigger Registration saga step if this is part of an onboarding flow.
        return Task.CompletedTask;
    }
}
