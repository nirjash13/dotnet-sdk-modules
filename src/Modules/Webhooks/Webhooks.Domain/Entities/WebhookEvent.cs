using System;
using SaasBuilder.SharedKernel.Tenancy;

namespace Webhooks.Domain.Entities;

/// <summary>A domain event queued for delivery to webhook endpoints.</summary>
public sealed class WebhookEvent : ITenantScoped
{
    private WebhookEvent()
    {
        // EF Core.
        EventType = string.Empty;
        PayloadJson = string.Empty;
    }

    /// <summary>Initializes a new webhook event.</summary>
    public WebhookEvent(
        Guid id,
        Guid tenantId,
        string eventType,
        string payloadJson,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("EventType must not be empty.", nameof(eventType));
        }

        Id = id;
        TenantId = tenantId;
        EventType = eventType;
        PayloadJson = payloadJson;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the event identifier (also used as the <c>webhook-id</c> header value).</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Gets the event type in dot-notation (e.g. "invoice.created").</summary>
    public string EventType { get; private set; }

    /// <summary>Gets the serialized JSON payload.</summary>
    public string PayloadJson { get; private set; }

    /// <summary>Gets when the event was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
