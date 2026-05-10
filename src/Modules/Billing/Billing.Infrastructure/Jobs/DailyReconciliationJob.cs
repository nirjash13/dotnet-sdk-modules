using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Billing.Infrastructure.Jobs;

/// <summary>
/// Background service that runs daily to detect drift between the local subscription database
/// and the external billing provider.
/// TODO(Phase 4): Implement reconciliation logic — query each provider for subscription state,
/// compare against local records, emit <see cref="Billing.Domain.DomainEvents.SubscriptionStatusChangedDomainEvent"/>
/// for any discrepancies, and alert operators on unresolved drift.
/// </summary>
public sealed class DailyReconciliationJob(ILogger<DailyReconciliationJob> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DailyReconciliationJob started. Interval: {Interval}h.", RunInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — do not log as error.
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

    private Task ReconcileAsync(CancellationToken ct)
    {
        // TODO(Phase 4): Query each configured IBillingProvider for subscription state,
        // compare against billing.subscriptions, and emit domain events for drift.
        logger.LogInformation("TODO(Phase 4): Daily reconciliation not yet implemented — skipping.");
        return Task.CompletedTask;
    }
}
