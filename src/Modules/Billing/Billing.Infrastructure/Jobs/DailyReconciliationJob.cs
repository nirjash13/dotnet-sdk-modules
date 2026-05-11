using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Domain.Entities;
using Billing.Domain.ValueObjects;
using Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;

namespace Billing.Infrastructure.Jobs;

/// <summary>
/// Background service that runs daily to detect drift between the local subscription database
/// and the external billing provider.
///
/// For each active local subscription that has a provider subscription ID, the job:
/// 1. Fetches the subscription status from the provider.
/// 2. Compares against the local status.
/// 3. Logs a structured warning for any drift detected.
/// 4. In a future phase, the warning is escalated to an alert (PagerDuty / OpsGenie).
///
/// Configurable via <c>Billing:Reconciliation:Enabled</c> (default: true).
/// </summary>
public sealed class DailyReconciliationJob(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DailyReconciliationJob> logger)
    : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    // Maps Stripe status strings to local SubscriptionStatus enum for comparison.
    private static readonly Dictionary<string, SubscriptionStatus> StatusMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["active"] = SubscriptionStatus.Active,
        ["trialing"] = SubscriptionStatus.Trialing,
        ["past_due"] = SubscriptionStatus.PastDue,
        ["canceled"] = SubscriptionStatus.Canceled,
        ["incomplete"] = SubscriptionStatus.Incomplete,
        ["paused"] = SubscriptionStatus.Paused,
    };

    /// <summary>
    /// Publicly callable reconciliation method — allows callers (tests, admin endpoints)
    /// to trigger reconciliation on demand without waiting for the 24h timer.
    /// </summary>
    public async Task RunReconciliationAsync(CancellationToken ct)
        => await ReconcileAsync(ct).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool enabled = configuration.GetValue("Billing:Reconciliation:Enabled", defaultValue: true);
        if (!enabled)
        {
            logger.LogInformation("DailyReconciliationJob is disabled via Billing:Reconciliation:Enabled=false.");
            return;
        }

        logger.LogInformation("DailyReconciliationJob started. Interval: {Interval}h.", RunInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in DailyReconciliationJob reconciliation cycle.");
            }

            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }

        logger.LogInformation("DailyReconciliationJob stopped.");
    }

    private async Task ReconcileAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting daily billing reconciliation.");

        using IServiceScope scope = serviceProvider.CreateScope();
        BillingDbContext db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        IBillingProvider billingProvider = scope.ServiceProvider.GetRequiredService<IBillingProvider>();

        // Fetch all subscriptions that have a provider ID and are not already canceled.
        List<Subscription> subscriptions = await db.Subscriptions
            .AsNoTracking()
            .Where(s => s.ProviderSubscriptionId != null && s.Status != SubscriptionStatus.Canceled)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        logger.LogInformation("Reconciling {Count} active subscriptions against provider.", subscriptions.Count);

        int driftCount = 0;

        foreach (Subscription sub in subscriptions)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            // ProviderSubscriptionId is guaranteed non-null by the query filter above.
            string providerId = sub.ProviderSubscriptionId!;

            Result<ProviderSubscriptionInfo?> providerResult = await billingProvider
                .GetSubscriptionAsync(providerId, ct)
                .ConfigureAwait(false);

            if (!providerResult.IsSuccess)
            {
                logger.LogWarning(
                    "Reconciliation: could not fetch subscription {ProviderId} from provider: {Error}",
                    providerId,
                    providerResult.Error);
                continue;
            }

            ProviderSubscriptionInfo? providerInfo = providerResult.Value;

            if (providerInfo is null)
            {
                logger.LogWarning(
                    "Reconciliation drift detected: subscription {SubscriptionId} (tenant={TenantId}) " +
                    "has provider_id={ProviderId} but the provider reports it does not exist.",
                    sub.Id,
                    sub.TenantId,
                    providerId);
                driftCount++;
                continue;
            }

            // Compare status.
            if (StatusMap.TryGetValue(providerInfo.Status, out SubscriptionStatus expectedStatus) &&
                expectedStatus != sub.Status)
            {
                logger.LogWarning(
                    "Reconciliation drift detected: subscription {SubscriptionId} " +
                    "(tenant={TenantId}, provider_id={ProviderId}) " +
                    "local_status={LocalStatus} provider_status={ProviderStatus}. " +
                    "Manual review or automated sync required.",
                    sub.Id,
                    sub.TenantId,
                    providerId,
                    sub.Status,
                    providerInfo.Status);
                driftCount++;
            }
        }

        logger.LogInformation(
            "Daily billing reconciliation complete. Checked: {Count}, Drift: {Drift}.",
            subscriptions.Count,
            driftCount);
    }
}
