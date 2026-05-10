using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Domain.Entities;
using Billing.Domain.Exceptions;
using Billing.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Commands;

/// <summary>
/// Handles <see cref="CancelSubscriptionCommand"/>.
/// Cancels the local subscription record; the provider-side call is coordinated separately
/// via the billing provider abstraction.
/// </summary>
public sealed class CancelSubscriptionHandler(
    ISubscriptionRepository subscriptions,
    IBillingProvider billingProvider,
    ILogger<CancelSubscriptionHandler> logger)
{
    /// <summary>Executes the command.</summary>
    public async Task<Result<bool>> ExecuteAsync(CancelSubscriptionCommand command, CancellationToken ct)
    {
        if (command is null)
        {
            return Result<bool>.Failure("Command must not be null.");
        }

        Subscription? subscription = await subscriptions
            .FindByTenantAsync(command.TenantId, ct)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return Result<bool>.Failure($"No active subscription found for tenant {command.TenantId}.");
        }

        // Cancel at the provider first (idempotent on provider side).
        if (!string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionId))
        {
            Result<bool> providerResult = await billingProvider
                .CancelSubscriptionAsync(subscription.ProviderSubscriptionId!, command.AtPeriodEnd, ct)
                .ConfigureAwait(false);

            if (!providerResult.IsSuccess)
            {
                return Result<bool>.Failure(
                    $"Provider cancellation failed: {providerResult.Error}");
            }
        }

        try
        {
            // If canceling at period end, the status remains Active until the webhook fires.
            // If canceling immediately, mark canceled now.
            if (!command.AtPeriodEnd)
            {
                subscription.Cancel(DateTimeOffset.UtcNow);
            }
        }
        catch (BillingDomainException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }

        subscriptions.Update(subscription);
        await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Subscription {SubscriptionId} canceled for tenant {TenantId} (atPeriodEnd={AtPeriodEnd}).",
            subscription.Id,
            command.TenantId,
            command.AtPeriodEnd);

        return Result<bool>.Success(true);
    }
}
