using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Billing.Application.Abstractions;
using Billing.Application.Jobs;
using Billing.Application.Options;
using Billing.Domain.Entities;
using Billing.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Jobs;

/// <summary>
/// Hosted service that periodically scans for tenants whose dunning grace period has elapsed
/// and suspends them by delegating to <see cref="SuspendTenantForUnpaidInvoiceJob"/>.
///
/// The service runs every hour. <see cref="BillingOptions.DunningGraceDays"/> controls the
/// grace window from the terminal payment failure timestamp to actual suspension.
/// </summary>
public sealed class DunningGraceHostedService(
    IServiceProvider serviceProvider,
    IOptions<BillingOptions> options,
    ILogger<DunningGraceHostedService> logger)
    : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "DunningGraceHostedService started. GraceDays={GraceDays}, ScanInterval={ScanInterval}.",
            options.Value.DunningGraceDays,
            ScanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(ScanInterval, stoppingToken).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RunScanAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunScanAsync(CancellationToken ct)
    {
        if (!options.Value.SuspendOnTerminalPaymentFailure)
        {
            return;
        }

        DateTimeOffset graceCutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.DunningGraceDays);

        try
        {
            // Each scan runs in its own scope to keep DbContext lifetime correct.
            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ISubscriptionRepository subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
            SuspendTenantForUnpaidInvoiceJob job = scope.ServiceProvider.GetRequiredService<SuspendTenantForUnpaidInvoiceJob>();

            IReadOnlyList<Subscription> overdue = await subscriptions
                .FindTerminalFailedBeforeAsync(graceCutoff, ct)
                .ConfigureAwait(false);

            if (overdue.Count == 0)
            {
                return;
            }

            logger.LogInformation("Dunning scan found {Count} tenants past grace period.", overdue.Count);

            foreach (Subscription sub in overdue)
            {
                try
                {
                    SaasBuilder.SharedKernel.Abstractions.Result result = await job
                        .ExecuteAsync(sub.TenantId, sub.TerminalFailedInvoiceId ?? "unknown", ct)
                        .ConfigureAwait(false);

                    if (!result.IsSuccess)
                    {
                        logger.LogWarning(
                            "Dunning job failed for tenant {TenantId}: {Error}",
                            sub.TenantId,
                            result.Error);
                    }
                    else
                    {
                        logger.LogInformation("Tenant {TenantId} suspended for overdue payment.", sub.TenantId);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Dunning job threw for tenant {TenantId}.", sub.TenantId);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "DunningGraceHostedService scan failed.");
        }
    }
}
