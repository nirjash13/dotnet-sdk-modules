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
/// Handles <see cref="PauseSubscriptionCommand"/> and <see cref="ResumeSubscriptionCommand"/>.
/// </summary>
public sealed class PauseResumeSubscriptionHandler(
    ISubscriptionRepository subscriptions,
    IBillingProvider billingProvider,
    ILogger<PauseResumeSubscriptionHandler> logger)
{
    /// <summary>Pauses the tenant's active subscription.</summary>
    public async Task<Result<bool>> PauseAsync(PauseSubscriptionCommand command, CancellationToken ct)
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

        if (!string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionId))
        {
            Result<bool> providerResult = await billingProvider
                .PauseSubscriptionAsync(subscription.ProviderSubscriptionId!, ct)
                .ConfigureAwait(false);

            if (!providerResult.IsSuccess)
            {
                return Result<bool>.Failure($"Provider pause failed: {providerResult.Error}");
            }
        }

        try
        {
            subscription.Pause(DateTimeOffset.UtcNow);
        }
        catch (BillingDomainException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }

        subscriptions.Update(subscription);
        await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Subscription {SubscriptionId} paused for tenant {TenantId}.", subscription.Id, command.TenantId);
        return Result<bool>.Success(true);
    }

    /// <summary>Resumes the tenant's paused subscription.</summary>
    public async Task<Result<bool>> ResumeAsync(ResumeSubscriptionCommand command, CancellationToken ct)
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

        if (!string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionId))
        {
            Result<bool> providerResult = await billingProvider
                .ResumeSubscriptionAsync(subscription.ProviderSubscriptionId!, ct)
                .ConfigureAwait(false);

            if (!providerResult.IsSuccess)
            {
                return Result<bool>.Failure($"Provider resume failed: {providerResult.Error}");
            }
        }

        try
        {
            subscription.Resume();
        }
        catch (BillingDomainException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }

        subscriptions.Update(subscription);
        await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Subscription {SubscriptionId} resumed for tenant {TenantId}.", subscription.Id, command.TenantId);
        return Result<bool>.Success(true);
    }
}
