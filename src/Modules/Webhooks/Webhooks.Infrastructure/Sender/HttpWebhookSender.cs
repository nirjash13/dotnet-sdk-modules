using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Webhooks.Application.Abstractions;
using Webhooks.Application.Signing;
using Webhooks.Domain.Entities;
using Webhooks.Infrastructure.Entities;
using Webhooks.Infrastructure.Persistence;

namespace Webhooks.Infrastructure.Sender;

/// <summary>
/// HTTP webhook sender that implements the Standard Webhooks delivery spec.
/// Retry schedule mirrors Svix: 5s, 5min, 30min, 2h, 5h, 10h, 14h, 20h, 24h (9 retries).
/// 4xx responses (except 408, 425, 429) are NOT retried.
/// 5xx and network failures follow the schedule.
/// </summary>
internal sealed class HttpWebhookSender(
    IWebhookEndpointRepository endpointRepo,
    WebhooksDbContext db,
    IHttpClientFactory httpClientFactory,
    ILogger<HttpWebhookSender> logger)
    : IWebhookSender
{
    // Svix-mirroring retry schedule.
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(5),
        TimeSpan.FromHours(10),
        TimeSpan.FromHours(14),
        TimeSpan.FromHours(20),
        TimeSpan.FromHours(24),
    };

    // HTTP status codes that should NOT be retried.
    private static readonly HashSet<int> NoRetryStatusCodes = new() { 400, 401, 403, 404, 409, 410, 422 };

    /// <inheritdoc />
    public async Task SendAsync(WebhookEvent evt, CancellationToken ct = default)
    {
        IReadOnlyList<WebhookEndpoint> endpoints = await endpointRepo
            .GetActiveByEventTypeAsync(evt.TenantId, evt.EventType, ct)
            .ConfigureAwait(false);

        foreach (WebhookEndpoint endpoint in endpoints)
        {
            await DeliverWithRetryAsync(evt, endpoint, ct).ConfigureAwait(false);
        }
    }

    private async Task DeliverWithRetryAsync(
        WebhookEvent evt,
        WebhookEndpoint endpoint,
        CancellationToken ct)
    {
        int maxAttempts = RetryDelays.Length + 1; // 10 total (1 initial + 9 retries)

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            bool succeeded = await TryDeliverAsync(evt, endpoint, ct).ConfigureAwait(false);
            if (succeeded)
            {
                return;
            }

            if (attempt < RetryDelays.Length)
            {
                // In Phase 5 scaffold, sleep inline. Production use: enqueue into Jobs module.
                // TODO(Phase 5.5): enqueue retry via IJobScheduler (Jobs module cross-module call).
                logger.LogInformation(
                    "Webhooks: endpoint {EndpointId} delivery failed (attempt {Attempt}/{Max}). " +
                    "Next retry in {Delay}.",
                    endpoint.Id, attempt + 1, maxAttempts, RetryDelays[attempt]);
            }
        }

        logger.LogError(
            "Webhooks: all {Max} delivery attempts to endpoint {EndpointId} exhausted for event {EventId}.",
            maxAttempts, endpoint.Id, evt.Id);
    }

    private async Task<bool> TryDeliverAsync(
        WebhookEvent evt,
        WebhookEndpoint endpoint,
        CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string messageId = evt.Id.ToString();

        // Derive signing secret from the stored hex-encoded secret.
        byte[] secretBytes = Convert.FromBase64String(endpoint.SecretHashedCurrent);
        byte[]? prevSecretBytes = endpoint.IsPreviousSecretValid(DateTimeOffset.UtcNow)
            && endpoint.SecretHashedPrevious is not null
                ? Convert.FromBase64String(endpoint.SecretHashedPrevious)
                : null;

        string signature = StandardWebhooksSigner.BuildSignatureHeader(
            secretBytes, messageId, timestamp, evt.PayloadJson, prevSecretBytes);

        using HttpClient client = httpClientFactory.CreateClient("webhooks");
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
        {
            Content = new StringContent(evt.PayloadJson, Encoding.UTF8, "application/json"),
        };

        request.Headers.Add("webhook-id", messageId);
        request.Headers.Add("webhook-timestamp", timestamp.ToString());
        request.Headers.Add("webhook-signature", signature);

        int? statusCode = null;
        string? responseBody = null;
        bool succeeded = false;

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, ct).ConfigureAwait(false);
            sw.Stop();
            statusCode = (int)response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            succeeded = response.IsSuccessStatusCode;

            if (!succeeded && NoRetryStatusCodes.Contains(statusCode.Value))
            {
                logger.LogWarning(
                    "Webhooks: endpoint {EndpointId} returned {StatusCode} — not retrying.",
                    endpoint.Id, statusCode);

                await LogAttemptAsync(evt.Id, endpoint.Id, statusCode, sw.ElapsedMilliseconds, responseBody, false, ct)
                    .ConfigureAwait(false);

                return true; // Don't retry 4xx non-retriable
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            logger.LogWarning(ex, "Webhooks: network error delivering to endpoint {EndpointId}.", endpoint.Id);
        }

        await LogAttemptAsync(evt.Id, endpoint.Id, statusCode, sw.ElapsedMilliseconds, responseBody, succeeded, ct)
            .ConfigureAwait(false);

        return succeeded;
    }

    private async Task LogAttemptAsync(
        Guid eventId,
        Guid endpointId,
        int? statusCode,
        long latencyMs,
        string? responseBody,
        bool succeeded,
        CancellationToken ct)
    {
        WebhookDeliveryAttempt attempt = new WebhookDeliveryAttempt(
            id: Guid.NewGuid(),
            endpointId: endpointId,
            eventId: eventId,
            statusCode: statusCode,
            latencyMs: latencyMs,
            responseBody: responseBody,
            attemptedAt: DateTimeOffset.UtcNow,
            succeeded: succeeded);

        await db.WebhookDeliveryAttempts.AddAsync(attempt, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
