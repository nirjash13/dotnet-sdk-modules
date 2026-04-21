using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Chassis.Host.Observability;

/// <summary>
/// Background service that periodically queries the MassTransit outbox table for undelivered
/// messages and records the delivery lag into the chassis meters.
/// </summary>
/// <remarks>
/// <para>
/// Queries <c>transport.outbox_message</c> (MassTransit EF Core Outbox schema) every
/// <see cref="PollInterval"/>. On each poll, records:
/// <list type="bullet">
///   <item><c>chassis.outbox.lag</c> — histogram: <c>EXTRACT(EPOCH FROM (NOW() - MIN(enqueue_time)))</c>
///         for undelivered rows; reflects worst-case delivery latency.</item>
/// </list>
/// </para>
/// <para>
/// Uses a raw Npgsql connection (from <c>ConnectionStrings:Chassis</c>) to avoid circular
/// EF Core DbContext lifetime dependencies. No tenant filter is applied — outbox rows are
/// not tenant-scoped.
/// </para>
/// <para>
/// <b>Failure policy:</b> A failed poll is logged as a warning and swallowed. Polling continues.
/// This prevents a transient DB outage from crashing the background service.
/// </para>
/// </remarks>
internal sealed class OutboxLagReporter : BackgroundService
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    // Use "chassis" as the module tag since this reporter covers the shared outbox.
    private const string ModuleTag = "chassis";

    // Lag SQL: seconds since the oldest undelivered row was enqueued.
    private const string LagSql =
        """
        SELECT EXTRACT(EPOCH FROM (NOW() - MIN(enqueue_time)))
        FROM transport.outbox_message
        WHERE delivered IS NULL
        """;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxLagReporter> _logger;

    public OutboxLagReporter(
        IServiceProvider serviceProvider,
        ILogger<OutboxLagReporter> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "OutboxLagReporter started; polling every {IntervalSeconds}s.",
            PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await PollAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Swallow to prevent background service crash on transient DB failure.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(
                    ex,
                    "OutboxLagReporter poll failed; will retry in {IntervalSeconds}s.",
                    PollInterval.TotalSeconds);
            }
        }

        _logger.LogInformation("OutboxLagReporter stopped.");
    }

    private async Task PollAsync(CancellationToken ct)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IConfiguration config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        string? connectionString = config.GetConnectionString("Chassis");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogDebug("OutboxLagReporter: ConnectionStrings:Chassis not configured; skipping poll.");
            return;
        }

        await using NpgsqlConnection connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using NpgsqlCommand lagCmd = new NpgsqlCommand(LagSql, connection);
        object? lagResult = await lagCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);

        if (lagResult is not DBNull && lagResult is not null)
        {
            double lagSeconds = Convert.ToDouble(lagResult);
            TagList tags = new TagList { { "module", ModuleTag } };
            ChassisMeters.OutboxLagSeconds.Record(lagSeconds, tags);

            _logger.LogDebug(
                "Outbox lag recorded: {LagSeconds:F3}s (module={Module})",
                lagSeconds,
                ModuleTag);
        }
    }
}
