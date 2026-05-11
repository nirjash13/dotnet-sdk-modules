using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Lifecycle;
using Identity.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Jobs;

/// <summary>
/// Background job that invokes <see cref="HardDeleteExpiredAccountsHandler"/> daily.
/// Scheduled by <see cref="HardDeleteJobScheduler"/>.
/// </summary>
public sealed class HardDeleteExpiredAccountsJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HardDeleteExpiredAccountsJob> _logger;

    /// <summary>Initializes a new instance of <see cref="HardDeleteExpiredAccountsJob"/>.</summary>
    public HardDeleteExpiredAccountsJob(
        IServiceProvider serviceProvider,
        ILogger<HardDeleteExpiredAccountsJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the hard-delete pass. Creates a DI scope so EF Core context is properly scoped.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HardDeleteExpiredAccountsJob started.");

        using IServiceScope scope = _serviceProvider.CreateScope();
        IUserRepository users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IUserTombstoneRepository tombstones = scope.ServiceProvider.GetRequiredService<IUserTombstoneRepository>();

        HardDeleteExpiredAccountsHandler handler = new HardDeleteExpiredAccountsHandler(users, tombstones);

        SaasBuilder.SharedKernel.Abstractions.Result<int> result =
            await handler.HandleAsync(new HardDeleteExpiredAccountsCommand(), cancellationToken)
                .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "HardDeleteExpiredAccountsJob completed. Hard-deleted {Count} accounts.",
                result.Value);
        }
        else
        {
            _logger.LogError("HardDeleteExpiredAccountsJob failed: {Error}", result.Error);
        }
    }
}
