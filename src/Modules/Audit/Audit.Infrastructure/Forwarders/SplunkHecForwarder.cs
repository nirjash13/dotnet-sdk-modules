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
/// Forwards audit events to a Splunk HTTP Event Collector (HEC) endpoint.
/// Events are buffered in a <see cref="Channel{T}"/> and forwarded by a background loop
/// to avoid blocking the caller. Silent-degrading: failures are logged, not propagated.
/// Config: <c>Audit:Forwarders:Splunk:{Url,Token}</c>.
/// </summary>
public sealed class SplunkHecForwarder : IAuditLogger, IHostedService, IAsyncDisposable
{
    private readonly SplunkHecOptions _opts;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SplunkHecForwarder> _logger;
    private readonly Channel<AuditEvent> _channel;
    private Task _backgroundTask = Task.CompletedTask;
    private CancellationTokenSource _cts = new CancellationTokenSource();

    /// <summary>Initializes a new instance of <see cref="SplunkHecForwarder"/>.</summary>
    public SplunkHecForwarder(
        IOptions<SplunkHecOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<SplunkHecForwarder> logger)
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
        // Fire-and-forget into the channel — never blocks the caller.
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

            // Drain up to 50 events per HTTP call.
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
            // Splunk HEC: each event is a JSON object with an "event" key.
            // Multiple events are newline-delimited.
            StringBuilder sb = new StringBuilder();
            foreach (AuditEvent evt in events)
            {
                object hecEvent = new
                {
                    time = (evt.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
                    @event = new
                    {
                        tenantId = evt.TenantId,
                        actorId = evt.ActorId,
                        action = evt.Action,
                        resourceType = evt.ResourceType,
                        resourceId = evt.ResourceId,
                        ipAddress = evt.IpAddress,
                        correlationId = evt.CorrelationId,
                    },
                };

                sb.Append(JsonSerializer.Serialize(hecEvent));
                sb.Append('\n');
            }

            using HttpClient client = _httpClientFactory.CreateClient("splunk-hec");
            using StringContent content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Splunk", _opts.Token);

            HttpResponseMessage response = await client.PostAsync(_opts.Url, content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Audit.SplunkHec: batch of {Count} events returned HTTP {Status}",
                    events.Count, (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Audit.SplunkHec: failed to forward {Count} audit events", events.Count);
        }
    }
}

/// <summary>Configuration options for the Splunk HEC forwarder.</summary>
public sealed class SplunkHecOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Audit:Forwarders:Splunk";

    /// <summary>Gets or sets the HEC endpoint URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the HEC token.</summary>
    public string Token { get; set; } = string.Empty;
}
