namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Marker interface for domain events.
/// Domain events represent something that happened within the domain boundary.
/// They are dispatched in-process and handled by one or more subscribers
/// within the same bounded context.
/// </summary>
public interface IDomainEvent
{
}
