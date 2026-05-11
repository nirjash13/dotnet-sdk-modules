using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Jobs;

/// <summary>
/// Hosted service that schedules <see cref="HardDeleteExpiredAccountsJob"/> to run
/// daily at 03:00 UTC using a lightweight timer loop (no external scheduler dependency).
/// </summary>
internal sealed class HardDeleteJobScheduler : BackgroundService
{
    private static readonly TimeSpan DailyInterval = TimeSpan.FromHours(24);
    private readonly HardDeleteExpiredAccountsJob _job;
    private readonly ILogger<HardDeleteJobScheduler> _logger;

    /// <summary>Initializes a new instance of <see cref="HardDeleteJobScheduler"/>.</summary>
    public HardDeleteJobScheduler(
        HardDeleteExpiredAccountsJob job,
        ILogger<HardDeleteJobScheduler> logger)
    {
        _job = job ?? throw new ArgumentNullException(nameof(job));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay = ComputeDelayUntilNextRun();
            _logger.LogInformation(
                "HardDeleteJobScheduler: next run in {Delay:hh\\:mm\\:ss}.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await _job.ExecuteAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HardDeleteJobScheduler: job execution threw an exception.");
            }
        }
    }

    private static TimeSpan ComputeDelayUntilNextRun()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset next = new DateTimeOffset(
            now.Year, now.Month, now.Day, 3, 0, 0, TimeSpan.Zero);

        if (next <= now)
        {
            next = next.Add(DailyInterval);
        }

        return next - now;
    }
}
