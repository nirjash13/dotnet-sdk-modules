using System.Threading;
using System.Threading.Tasks;

namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Abstraction over the in-process mediator and out-of-process bus.
/// Implementations route messages either through MassTransit Mediator (in-proc)
/// or MassTransit RabbitMQ transport (out-of-proc) based on host configuration.
/// Business code never references MassTransit directly — only this interface.
/// </summary>
public interface IModuleDispatcher
{
    /// <summary>
    /// Sends a request and awaits a typed response.
    /// Used for commands and queries that require a reply.
    /// </summary>
    /// <typeparam name="TRequest">The request message type. Must be a reference type.</typeparam>
    /// <typeparam name="TResponse">The response message type. Must be a reference type.</typeparam>
    /// <param name="request">The request message to dispatch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response produced by the handler.</returns>
    Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct = default)
        where TRequest : class
        where TResponse : class;

    /// <summary>
    /// Publishes an event to all registered subscribers.
    /// Used for domain events (in-proc) and integration events (in-proc or bus).
    /// Fire-and-forget from the publisher's perspective; delivery guarantees depend
    /// on the configured transport.
    /// </summary>
    /// <typeparam name="TEvent">The event type. Must be a reference type.</typeparam>
    /// <param name="evt">The event to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
        where TEvent : class;
}
