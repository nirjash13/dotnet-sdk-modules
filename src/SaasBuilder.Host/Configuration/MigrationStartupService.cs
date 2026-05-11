using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaasBuilder.Persistence.Migrations;

namespace SaasBuilder.Host.Configuration;

/// <summary>
/// <see cref="IHostedService"/> that runs pending EF Core migrations via
/// <see cref="IMigrationRunner"/> on application startup.
/// </summary>
/// <remarks>
/// Registered when <c>opts.Persistence.MigrateOnStartup = true</c> is set on
/// <see cref="Options.SaasBuilderOptions.Persistence"/>. The migration runner acquires
/// a Postgres advisory lock to ensure only one instance runs migrations during
/// rolling deployments.
/// </remarks>
internal sealed class MigrationStartupService : IHostedService
{
    private readonly IMigrationRunner _runner;
    private readonly ILogger<MigrationStartupService> _logger;

    public MigrationStartupService(
        IMigrationRunner runner,
        ILogger<MigrationStartupService> logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MigrateOnStartup is enabled — running pending migrations.");
        try
        {
            await _runner.RunPendingAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Startup migrations completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup migrations failed. The application will continue; some features may be degraded.");

            // Do not rethrow — a migration failure should not prevent the app from starting
            // (other instances or manual intervention can recover). Callers can override by
            // registering a custom exception filter in their hosted service.
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
