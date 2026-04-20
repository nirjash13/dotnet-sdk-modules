namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Marker interface for integration events.
/// Integration events cross bounded-context boundaries and may travel over
/// a message bus (e.g., RabbitMQ via MassTransit). They are wrapped in a
/// <see cref="Contracts.CloudEventEnvelope"/> when crossing non-.NET boundaries.
/// </summary>
public interface IIntegrationEvent
{
}
