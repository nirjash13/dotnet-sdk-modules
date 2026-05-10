using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Webhooks.Contracts;

namespace Webhooks.Application.Abstractions;

/// <summary>Read model for webhook delivery logs.</summary>
public interface IWebhookDeliveryQuery
{
    /// <summary>Returns paginated delivery attempts for the specified endpoint.</summary>
    Task<(int Total, IReadOnlyList<WebhookDeliveryDto> Items)> QueryAsync(
        Guid? endpointId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
