using System;

namespace Webhooks.Contracts;

/// <summary>DTO for a single delivery attempt returned by the delivery log API.</summary>
/// <param name="Id">Delivery attempt identifier.</param>
/// <param name="EndpointId">Target endpoint identifier.</param>
/// <param name="EventId">Webhook event identifier.</param>
/// <param name="StatusCode">HTTP status code returned by the recipient.</param>
/// <param name="LatencyMs">Round-trip latency in milliseconds.</param>
/// <param name="ResponseBody">Truncated response body (first 1 KB).</param>
/// <param name="AttemptedAt">When this delivery attempt occurred.</param>
/// <param name="Succeeded">Whether the delivery was successful (2xx response).</param>
public record WebhookDeliveryDto(
    Guid Id,
    Guid EndpointId,
    Guid EventId,
    int? StatusCode,
    long? LatencyMs,
    string? ResponseBody,
    DateTimeOffset AttemptedAt,
    bool Succeeded);
