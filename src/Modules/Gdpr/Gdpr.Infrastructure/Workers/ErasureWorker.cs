using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gdpr.Application.Abstractions;
using Gdpr.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gdpr.Infrastructure.Workers;

/// <summary>
/// Nightly background worker that processes overdue erasure requests by calling
/// all registered <see cref="IErasureHandler"/> implementations per module.
/// </summary>
public sealed class ErasureWorker : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ErasureWorker> _logger;

    /// <summary>Initializes a new instance of <see cref="ErasureWorker"/>.</summary>
    public ErasureWorker(IServiceScopeFactory scopeFactory, ILogger<ErasureWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay first run slightly so the host can finish startup.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(RunInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

            var erasureRepo = scope.ServiceProvider.GetRequiredService<IGdprErasureRepository>();
            IEnumerable<IErasureHandler> handlers = scope.ServiceProvider.GetServices<IErasureHandler>();

            IReadOnlyList<ErasureRequestDto> overdue = await erasureRepo
                .GetOverdueAsync(DateTimeOffset.UtcNow, ct)
                .ConfigureAwait(false);

            foreach (ErasureRequestDto request in overdue)
            {
                _logger.LogInformation(
                    "Processing erasure request {RequestId} for user {UserId} in tenant {TenantId}",
                    request.Id,
                    request.UserId,
                    request.TenantId);

                foreach (IErasureHandler handler in handlers)
                {
                    try
                    {
                        await handler.AnonymizeAsync(request.TenantId, request.UserId, ct)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Erasure handler {Handler} failed for request {RequestId}",
                            handler.GetType().Name,
                            request.Id);
                    }
                }

                await erasureRepo.MarkCompletedAsync(request.Id, DateTimeOffset.UtcNow, ct)
                    .ConfigureAwait(false);

                _logger.LogInformation("Erasure request {RequestId} completed", request.Id);
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — exit cleanly.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erasure worker cycle failed");
        }
    }
}
