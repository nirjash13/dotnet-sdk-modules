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
/// Handles <see cref="CreateSubscriptionCommand"/> — persists a new subscription record
/// after the provider confirms checkout completion.
/// </summary>
public sealed class CreateSubscriptionHandler(
    ISubscriptionRepository subscriptions,
    ILogger<CreateSubscriptionHandler> logger)
{
    /// <summary>Executes the command.</summary>
    public async Task<Result<Guid>> ExecuteAsync(CreateSubscriptionCommand command, CancellationToken ct)
    {
        if (command is null)
        {
            return Result<Guid>.Failure("Command must not be null.");
        }

        if (command.TenantId == Guid.Empty)
        {
            return Result<Guid>.Failure("TenantId is required.");
        }

        if (command.PlanId == Guid.Empty)
        {
            return Result<Guid>.Failure("PlanId is required.");
        }

        // Check for an existing active subscription.
        Subscription? existing = await subscriptions
            .FindByTenantAsync(command.TenantId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Result<Guid>.Failure(
                $"Tenant {command.TenantId} already has a subscription ({existing.Id}). Cancel or upgrade instead.");
        }

        Subscription subscription;
        try
        {
            subscription = Subscription.Create(
                command.TenantId,
                command.PlanId,
                command.ProviderSubscriptionId,
                command.TrialEndsAt);
        }
        catch (BillingDomainException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }

        await subscriptions.AddAsync(subscription, ct).ConfigureAwait(false);
        await subscriptions.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "Subscription {SubscriptionId} created for tenant {TenantId} on plan {PlanId}.",
            subscription.Id,
            command.TenantId,
            command.PlanId);

        return Result<Guid>.Success(subscription.Id);
    }
}
