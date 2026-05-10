using System;
using System.Collections.Generic;
using SaasBuilder.SharedKernel.Tenancy;

namespace Webhooks.Domain.Entities;

/// <summary>Status of a webhook endpoint.</summary>
public enum WebhookEndpointStatus
{
    /// <summary>Actively receiving deliveries.</summary>
    Active = 0,

    /// <summary>Delivery paused (e.g. too many failures).</summary>
    Paused = 1,

    /// <summary>Endpoint has been deleted.</summary>
    Deleted = 2,
}

/// <summary>
/// A tenant-owned webhook endpoint. Delivery is attempted for all subscribed event types.
/// Secret rotation: <see cref="SecretHashedCurrent"/> is the active signing secret;
/// <see cref="SecretHashedPrevious"/> remains valid for 24 hours after rotation.
/// </summary>
public sealed class WebhookEndpoint : ITenantScoped
{
    private readonly List<string> _eventTypes = new();

    private WebhookEndpoint()
    {
        // EF Core.
        Url = string.Empty;
        SecretHashedCurrent = string.Empty;
    }

    /// <summary>Initializes a new webhook endpoint.</summary>
    public WebhookEndpoint(
        Guid id,
        Guid tenantId,
        string url,
        string? description,
        IEnumerable<string> eventTypes,
        string secretHashedCurrent,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Webhook URL must not be empty.", nameof(url));
        }

        if (string.IsNullOrWhiteSpace(secretHashedCurrent))
        {
            throw new ArgumentException("Secret must not be empty.", nameof(secretHashedCurrent));
        }

        Id = id;
        TenantId = tenantId;
        Url = url;
        Description = description;
        SecretHashedCurrent = secretHashedCurrent;
        Status = WebhookEndpointStatus.Active;
        CreatedAt = createdAt;
        _eventTypes.AddRange(eventTypes);
    }

    /// <summary>Gets the endpoint identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc />
    public Guid TenantId { get; private set; }

    /// <summary>Gets the delivery URL.</summary>
    public string Url { get; private set; }

    /// <summary>Gets the human-readable description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the current signing secret (HMAC-SHA256 base64 raw bytes).</summary>
    public string SecretHashedCurrent { get; private set; }

    /// <summary>Gets the previous signing secret (valid for 24 hours after rotation).</summary>
    public string? SecretHashedPrevious { get; private set; }

    /// <summary>Gets the timestamp when secret rotation occurred.</summary>
    public DateTimeOffset? SecretRotatedAt { get; private set; }

    /// <summary>Gets the subscribed event types.</summary>
    public IReadOnlyList<string> EventTypes => _eventTypes.AsReadOnly();

    /// <summary>Gets the endpoint status.</summary>
    public WebhookEndpointStatus Status { get; private set; }

    /// <summary>Gets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Rotates the secret. The previous secret remains valid for 24 hours.</summary>
    public void RotateSecret(string newSecretHashed, DateTimeOffset rotatedAt)
    {
        SecretHashedPrevious = SecretHashedCurrent;
        SecretHashedCurrent = newSecretHashed;
        SecretRotatedAt = rotatedAt;
    }

    /// <summary>Returns whether the previous secret is still in its 24-hour grace period.</summary>
    public bool IsPreviousSecretValid(DateTimeOffset now)
        => SecretHashedPrevious is not null
            && SecretRotatedAt.HasValue
            && now - SecretRotatedAt.Value < TimeSpan.FromHours(24);

    /// <summary>Sets the event types subscribed by this endpoint.</summary>
    public void SetEventTypes(IEnumerable<string> types)
    {
        _eventTypes.Clear();
        _eventTypes.AddRange(types);
    }

    /// <summary>Marks the endpoint as deleted.</summary>
    public void Delete() => Status = WebhookEndpointStatus.Deleted;
}
