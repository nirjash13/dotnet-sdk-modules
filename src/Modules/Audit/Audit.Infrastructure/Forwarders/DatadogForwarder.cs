using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Application.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Audit.Infrastructure.Forwarders;

/// <summary>
/// Forwards audit events to the Datadog Log Management API.
/// Events are buffered in a <see cref="Channel{T}"/> and forwarded by a background loop.
/// Silent-degrading: failures are logged, not propagated.
/// Config: <c>Audit:Forwarders:Datadog:{ApiKey,Site}</c>.
/// </summary>
public sealed class DatadogForwarder : IAuditLogger, IHostedService, IAsyncDisposable
{
    private readonly DatadogForwarderOptions _opts;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DatadogForwarder> _logger;
    private readonly Channel<AuditEvent> _channel;
    private Task _backgroundTask = Task.CompletedTask;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    /// <summary>Initializes a new instance of <see cref="DatadogForwarder"/>.</summary>
    public DatadogForwarder(
        IOptions<DatadogForwarderOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<DatadogForwarder> logger)
    {
        _opts = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<AuditEvent>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
    }

    /// <inheritdoc />
    public async Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _backgroundTask = Task.Run(() => DrainLoopAsync(_cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _backgroundTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _cts.Dispose();
        await _backgroundTask.ConfigureAwait(false);
    }

    private async Task DrainLoopAsync(CancellationToken ct)
    {
        List<AuditEvent> batch = new List<AuditEvent>(50);

        await foreach (AuditEvent evt in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            batch.Add(evt);

            while (batch.Count < 50 && _channel.Reader.TryRead(out AuditEvent? next))
            {
                batch.Add(next);
            }

            await FlushBatchAsync(batch, ct).ConfigureAwait(false);
            batch.Clear();
        }
    }

    private async Task FlushBatchAsync(List<AuditEvent> events, CancellationToken ct)
    {
        try
        {
            // Datadog Logs API: POST https://http-intake.logs.{site}/api/v2/logs
            string endpoint = $"https://http-intake.logs.{_opts.Site}/api/v2/logs";

            List<object> logs = new List<object>(events.Count);
            foreach (AuditEvent evt in events)
            {
                logs.Add(new
                {
                    ddsource = "saasbuilder-audit",
                    ddtags = $"tenant:{evt.TenantId},action:{evt.Action}",
                    hostname = "saasbuilder",
                    message = evt.Action,
                    tenantId = evt.TenantId,
                    actorId = evt.ActorId,
                    resourceType = evt.ResourceType,
                    resourceId = evt.ResourceId,
                    ipAddress = evt.IpAddress,
                    correlationId = evt.CorrelationId,
                    timestamp = (evt.Timestamp ?? DateTimeOffset.UtcNow).ToString("O"),
                });
            }

            string json = JsonSerializer.Serialize(logs);
            using HttpClient client = _httpClientFactory.CreateClient("datadog-logs");
            using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Add("DD-API-KEY", _opts.ApiKey);

            HttpResponseMessage response = await client.PostAsync(endpoint, content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Audit.Datadog: batch of {Count} events returned HTTP {Status}",
                    events.Count, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Audit.Datadog: failed to forward {Count} audit events", events.Count);
        }
    }
}

/// <summary>Configuration options for the Datadog forwarder.</summary>
public sealed class DatadogForwarderOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Audit:Forwarders:Datadog";

    /// <summary>Gets or sets the Datadog API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the Datadog site (e.g. "datadoghq.com" or "datadoghq.eu").</summary>
    public string Site { get; set; } = "datadoghq.com";
}
