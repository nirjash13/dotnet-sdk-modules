using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Webhooks.Domain.Entities;

namespace Webhooks.Application.Abstractions;

/// <summary>Repository for <see cref="WebhookEndpoint"/> entities.</summary>
public interface IWebhookEndpointRepository
{
    /// <summary>Adds a new endpoint.</summary>
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default);

    /// <summary>Returns all active endpoints for a tenant subscribed to the given event type.</summary>
    Task<IReadOnlyList<WebhookEndpoint>> GetActiveByEventTypeAsync(
        Guid tenantId,
        string eventType,
        CancellationToken ct = default);

    /// <summary>Returns all endpoints for a tenant.</summary>
    Task<IReadOnlyList<WebhookEndpoint>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Returns a single endpoint by id, or null if not found.</summary>
    Task<WebhookEndpoint?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persists changes.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
