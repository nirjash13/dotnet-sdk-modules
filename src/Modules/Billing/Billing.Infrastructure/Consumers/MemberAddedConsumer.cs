using System;
using System.Threading.Tasks;
using Billing.Application.Services;
using Billing.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Billing.Infrastructure.Consumers;

/// <summary>
/// MassTransit consumer that forwards <see cref="MemberAddedIntegrationEvent"/> to
/// <see cref="SeatSyncService"/> so the billing provider subscription quantity is updated
/// whenever a member is added to an organization.
/// </summary>
/// <remarks>
/// Register this consumer by calling
/// <c>BillingInfrastructureExtensions.AddBillingSeatSyncConsumers(configurator)</c>
/// inside the <c>AddSaasBuilderHost</c> MassTransit consumer callback.
/// </remarks>
public sealed class MemberAddedConsumer(
    SeatSyncService seatSyncService,
    ILogger<MemberAddedConsumer> logger) : IConsumer<MemberAddedIntegrationEvent>
{
    /// <inheritdoc />
    public async Task Consume(ConsumeContext<MemberAddedIntegrationEvent> context)
    {
        MemberAddedIntegrationEvent @event = context.Message;

        Result<SeatSyncOutcome> result = await seatSyncService.OnMemberAddedAsync(
            @event,
            softLimitSeats: null,
            hardLimitSeats: null,
            context.CancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            logger.LogError(
                "SeatSyncService failed for MemberAdded (tenantId={TenantId}, memberId={MemberId}): {Error}",
                @event.TenantId,
                @event.MemberId,
                result.Error);

            // Throw to let MassTransit retry/dead-letter the message rather than silently dropping it.
            throw new InvalidOperationException(
                $"Seat sync failed for tenant {@event.TenantId}: {result.Error}");
        }

        logger.LogInformation(
            "Seat sync completed for MemberAdded (tenantId={TenantId}, memberId={MemberId}, seats={Seats}).",
            @event.TenantId,
            @event.MemberId,
            result.Value!.CurrentSeats);
    }
}
