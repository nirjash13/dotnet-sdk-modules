using System;
using System.Threading.Tasks;
using Billing.Application.Services;
using Billing.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Consumers;

/// <summary>
/// MassTransit consumer that forwards <see cref="MemberRemovedIntegrationEvent"/> to
/// <see cref="SeatSyncService"/> so the billing provider subscription quantity is decremented
/// whenever a member is removed from an organization.
/// </summary>
/// <remarks>
/// Register this consumer by calling
/// <c>BillingInfrastructureExtensions.AddBillingSeatSyncConsumers(configurator)</c>
/// inside the <c>AddSaasBuilderHost</c> MassTransit consumer callback.
/// </remarks>
public sealed class MemberRemovedConsumer(
    SeatSyncService seatSyncService,
    ILogger<MemberRemovedConsumer> logger) : IConsumer<MemberRemovedIntegrationEvent>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<MemberRemovedIntegrationEvent> context)
    {
        MemberRemovedIntegrationEvent @event = context.Message;

        Result<SeatSyncOutcome> result = await seatSyncService.OnMemberRemovedAsync(
            @event,
            context.CancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            logger.LogError(
                "SeatSyncService failed for MemberRemoved (tenantId={TenantId}, memberId={MemberId}): {Error}",
                @event.TenantId,
                @event.MemberId,
                result.Error);

            throw new InvalidOperationException(
                $"Seat sync failed for tenant {@event.TenantId}: {result.Error}");
        }

        logger.LogInformation(
            "Seat sync completed for MemberRemoved (tenantId={TenantId}, memberId={MemberId}, seats={Seats}).",
            @event.TenantId,
            @event.MemberId,
            result.Value!.CurrentSeats);
    }
}
