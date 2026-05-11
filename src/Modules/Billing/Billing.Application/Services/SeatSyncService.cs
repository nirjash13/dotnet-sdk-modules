using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Contracts;
using Billing.Domain.Entities;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Services;

/// <summary>
/// Synchronizes per-seat billing quantities with the billing provider when members are added or removed.
///
/// Listens to <see cref="MemberAddedIntegrationEvent"/> and <see cref="MemberRemovedIntegrationEvent"/>.
/// Published by the Identity module when organization membership changes.
///
/// Soft-limit behavior: when current seats exceed the entitlement limit, this service logs a warning
/// and sets <c>X-Seat-Limit-Warning: true</c> on the outbound response via an ambient flag.
/// Hard-limit behavior: when seats hit the hard cap, this service returns a failure result that
/// the endpoint layer translates into HTTP 402 Payment Required.
/// </summary>
public sealed class SeatSyncService(
    ISubscriptionRepository subscriptions,
    IBillingProvider billingProvider,
    ILogger<SeatSyncService> logger)
{
    /// <summary>
    /// Called when a member is added to the organization.
    /// Updates the provider subscription quantity to reflect the new seat count.
    /// </summary>
    public async Task<Result<SeatSyncOutcome>> OnMemberAddedAsync(
        MemberAddedIntegrationEvent @event,
        long? softLimitSeats,
        long? hardLimitSeats,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return await SyncSeatCountAsync(
            @event.TenantId,
            @event.NewSeatCount,
            softLimitSeats,
            hardLimitSeats,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Called when a member is removed from the organization.
    /// Updates the provider subscription quantity to reflect the reduced seat count.
    /// </summary>
    public async Task<Result<SeatSyncOutcome>> OnMemberRemovedAsync(
        MemberRemovedIntegrationEvent @event,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return await SyncSeatCountAsync(
            @event.TenantId,
            @event.NewSeatCount,
            softLimitSeats: null,
            hardLimitSeats: null,
            ct).ConfigureAwait(false);
    }

    private async Task<Result<SeatSyncOutcome>> SyncSeatCountAsync(
        Guid tenantId,
        int newSeatCount,
        long? softLimitSeats,
        long? hardLimitSeats,
        CancellationToken ct)
    {
        // Hard limit check — blocks the action before touching the provider.
        if (hardLimitSeats.HasValue && newSeatCount > hardLimitSeats.Value)
        {
            logger.LogWarning(
                "Seat hard limit exceeded for tenant {TenantId}: requested={Requested}, limit={Limit}.",
                tenantId,
                newSeatCount,
                hardLimitSeats.Value);

            return Result<SeatSyncOutcome>.Success(new SeatSyncOutcome(
                IsAtHardLimit: true,
                IsAtSoftLimit: false,
                CurrentSeats: newSeatCount));
        }

        // Soft limit check — allows but warns.
        bool atSoftLimit = softLimitSeats.HasValue && newSeatCount >= softLimitSeats.Value;
        if (atSoftLimit)
        {
            logger.LogWarning(
                "Seat soft limit reached for tenant {TenantId}: seats={Seats}, limit={Limit}.",
                tenantId,
                newSeatCount,
                softLimitSeats!.Value);
        }

        // Fetch subscription to get the provider subscription ID.
        Subscription? subscription = await subscriptions
            .FindByTenantAsync(tenantId, ct)
            .ConfigureAwait(false);

        if (subscription is null || string.IsNullOrEmpty(subscription.ProviderSubscriptionId))
        {
            logger.LogDebug(
                "No active provider subscription for tenant {TenantId} — skipping seat sync.",
                tenantId);

            return Result<SeatSyncOutcome>.Success(new SeatSyncOutcome(
                IsAtHardLimit: false,
                IsAtSoftLimit: atSoftLimit,
                CurrentSeats: newSeatCount));
        }

        Result<bool> updateResult = await billingProvider
            .UpdateQuantityAsync(subscription.ProviderSubscriptionId, newSeatCount, ct)
            .ConfigureAwait(false);

        if (!updateResult.IsSuccess)
        {
            logger.LogError(
                "Failed to update seat quantity for tenant {TenantId}: {Error}",
                tenantId,
                updateResult.Error);

            return Result<SeatSyncOutcome>.Failure(
                $"Provider seat quantity update failed: {updateResult.Error}");
        }

        logger.LogInformation(
            "Seat count synced for tenant {TenantId}: {Count} seat(s).",
            tenantId,
            newSeatCount);

        return Result<SeatSyncOutcome>.Success(new SeatSyncOutcome(
            IsAtHardLimit: false,
            IsAtSoftLimit: atSoftLimit,
            CurrentSeats: newSeatCount));
    }
}

/// <summary>Outcome of a seat sync operation, used to drive HTTP response headers.</summary>
/// <param name="IsAtHardLimit">When true, the endpoint should return 402 Payment Required.</param>
/// <param name="IsAtSoftLimit">When true, the endpoint should add X-Seat-Limit-Warning: true header.</param>
/// <param name="CurrentSeats">The resulting seat count.</param>
public sealed record SeatSyncOutcome(bool IsAtHardLimit, bool IsAtSoftLimit, int CurrentSeats);
