using System;
using System.Collections.Generic;

namespace Webhooks.Contracts;

/// <summary>DTO for a webhook endpoint returned by the API.</summary>
/// <param name="Id">Endpoint identifier.</param>
/// <param name="Url">Delivery URL.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="EventTypes">Subscribed event types (e.g. "invoice.created").</param>
/// <param name="Status">Endpoint status.</param>
/// <param name="CreatedAt">When the endpoint was created.</param>
public record WebhookEndpointDto(
    Guid Id,
    string Url,
    string? Description,
    IReadOnlyList<string> EventTypes,
    string Status,
    DateTimeOffset CreatedAt);
