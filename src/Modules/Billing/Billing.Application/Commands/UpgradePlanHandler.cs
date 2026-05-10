using System;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Domain.Entities;
using Billing.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Application.Commands;

/// <summary>
/// Handles <see cref="UpgradePlanCommand"/>.
/// </summary>
public sealed class UpgradePlanHandler(
    ISubscriptionRepository subscriptions,
    IBillingProvider billingProvider,
    ILogger<UpgradePlanHandler> logger)
{
    /// <summary>Executes the command.</summary>
    public async Task<Result<bool>> ExecuteAsync(UpgradePlanCommand command, CancellationToken ct)
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
            return Result<bool>.Failure($"No subscription found for tenant {command.TenantId}.");
        }

        if (!string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionId) &&
            !string.IsNullOrWhiteSpace(command.NewPriceId))
        {
            Result<bool> providerResult = await billingProvider
                .UpgradeSubscriptionAsync(subscription.ProviderSubscriptionId!, command.NewPriceId, ct)
                .ConfigureAwait(false);

            if (!providerResult.IsSuccess)
            {
                return Result<bool>.Failure($"Provider upgrade failed: {providerResult.Error}");
            }
        }

        try
        {
            subscription.UpgradePlan(command.NewPlanId);
        }
        catch (BillingDomainException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }

        subscriptions.Update(subscription);
        await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Subscription {SubscriptionId} upgraded to plan {NewPlanId} for tenant {TenantId}.",
            subscription.Id,
            command.NewPlanId,
            command.TenantId);

        return Result<bool>.Success(true);
    }
}
