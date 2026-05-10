using System;

namespace Webhooks.Infrastructure.Entities;

/// <summary>
/// Persisted record of a single delivery attempt to a webhook endpoint.
/// Immutable after creation — append-only.
/// </summary>
public sealed class WebhookDeliveryAttempt
{
    private WebhookDeliveryAttempt()
    {
        // EF Core.
    }

    /// <summary>Initializes a new delivery attempt record.</summary>
    public WebhookDeliveryAttempt(
        Guid id,
        Guid endpointId,
        Guid eventId,
        int? statusCode,
        long? latencyMs,
        string? responseBody,
        DateTimeOffset attemptedAt,
        bool succeeded)
    {
        Id = id;
        EndpointId = endpointId;
        EventId = eventId;
        StatusCode = statusCode;
        LatencyMs = latencyMs;
        ResponseBody = responseBody?.Length > 1024 ? responseBody[..1024] : responseBody;
        AttemptedAt = attemptedAt;
        Succeeded = succeeded;
    }

    /// <summary>Gets the attempt identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the target endpoint identifier.</summary>
    public Guid EndpointId { get; private set; }

    /// <summary>Gets the source event identifier.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Gets the HTTP status code returned by the endpoint; null on network failure.</summary>
    public int? StatusCode { get; private set; }

    /// <summary>Gets the round-trip latency in milliseconds; null on network failure.</summary>
    public long? LatencyMs { get; private set; }

    /// <summary>Gets the response body (truncated to 1 KB).</summary>
    public string? ResponseBody { get; private set; }

    /// <summary>Gets when this attempt was made.</summary>
    public DateTimeOffset AttemptedAt { get; private set; }

    /// <summary>Gets whether the delivery was acknowledged (2xx).</summary>
    public bool Succeeded { get; private set; }
}
