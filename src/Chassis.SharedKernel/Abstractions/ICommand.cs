namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Marker interface for commands that produce no response value.
/// Commands represent an intent to change state. They are dispatched via
/// <see cref="IModuleDispatcher"/> and handled by exactly one consumer.
/// </summary>
public interface ICommand
{
}

/// <summary>
/// Marker interface for commands that produce a typed response.
/// </summary>
/// <typeparam name="TResponse">The type of the command's response.</typeparam>
public interface ICommand<TResponse>
{
}
