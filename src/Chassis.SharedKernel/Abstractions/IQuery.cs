namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Marker interface for queries that return a typed result.
/// Queries must not produce side effects; they are safe to retry and cache.
/// </summary>
/// <typeparam name="TResponse">The type of the query result.</typeparam>
public interface IQuery<TResponse>
{
}
