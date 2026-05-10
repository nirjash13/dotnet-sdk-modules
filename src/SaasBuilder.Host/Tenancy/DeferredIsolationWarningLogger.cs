using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Configuration;

namespace SaasBuilder.Host.Tenancy;

/// <summary>
/// Carries the deferred isolation mode that needs a startup warning.
/// </summary>
internal sealed class DeferredIsolationWarning
{
    /// <summary>Initializes the warning record with the configured isolation mode.</summary>
    public DeferredIsolationWarning(TenantIsolation isolation)
    {
        Isolation = isolation;
    }

    /// <summary>Gets the isolation mode that is configured but not yet implemented.</summary>
    public TenantIsolation Isolation { get; }
}

/// <summary>
/// Hosted service that emits a one-time startup warning when a deferred tenant isolation
/// mode is configured. This approach is used because <see cref="ILogger{T}"/> is not
/// available during DI registration; hosted services run after the container is built.
/// </summary>
internal sealed class DeferredIsolationWarningLogger : IHostedService
{
    private readonly DeferredIsolationWarning _warning;
    private readonly ILogger<DeferredIsolationWarningLogger> _logger;

    public DeferredIsolationWarningLogger(
        DeferredIsolationWarning warning,
        ILogger<DeferredIsolationWarningLogger> logger)
    {
        _warning = warning;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Tenancy mode {Mode} is configured as deferred — use PoolWithRls for v0.x. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md.",
            _warning.Isolation);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
