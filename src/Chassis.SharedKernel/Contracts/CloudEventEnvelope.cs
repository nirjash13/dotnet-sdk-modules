using System;

namespace Chassis.SharedKernel.Contracts;

/// <summary>
/// A CloudEvents 1.0-compliant envelope for integration events that cross non-.NET boundaries.
/// Used by <c>Integration.CloudEventsAdapter</c> when publishing events to non-.NET consumers
/// and by <c>Integration.Framework48Bridge</c> when converting legacy SOAP messages.
/// See https://cloudevents.io/ and https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md.
/// </summary>
/// <remarks>
/// Phase-0 fix: all mutable properties are now <c>init</c>-only to prevent partial mutation
/// after construction. <c>Time</c> is set via constructor default (caller-supplied) rather
/// than a property initializer, so the timestamp is captured at construction time, not at
/// first-access time, avoiding round-trip drift when the object is constructed early and
/// serialized later.
/// </remarks>
public sealed class CloudEventEnvelope
{
    /// <summary>Initializes a new instance of <see cref="CloudEventEnvelope"/>.</summary>
    /// <param name="id">Unique event identifier (UUID recommended).</param>
    /// <param name="source">URI identifying the event producer (e.g., <c>/chassis/ledger</c>).</param>
    /// <param name="type">Fully-qualified event type name (e.g., <c>com.chassis.ledger.transaction.posted.v1</c>).</param>
    /// <param name="data">The event payload. May be any serializable object.</param>
    /// <param name="time">
    /// UTC timestamp when this event occurred. Defaults to <c>DateTimeOffset.UtcNow</c>
    /// at call time so the clock is captured once at construction, not deferred.
    /// </param>
    public CloudEventEnvelope(
        string id,
        string source,
        string type,
        object? data,
        DateTimeOffset? time = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Data = data;
        Time = time ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets the CloudEvents <c>specversion</c> attribute.
    /// Always <c>"1.0"</c> for this implementation.
    /// </summary>
    public string SpecVersion { get; init; } = "1.0";

    /// <summary>
    /// Gets the unique identifier for this event occurrence.
    /// Combined with <see cref="Source"/>, this forms a globally unique event identity.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the URI-reference identifying the context in which this event occurred
    /// (e.g., the service name or bounded context path).
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the fully-qualified event type identifier, following reverse-domain notation.
    /// Consumers use this to dispatch the envelope to the correct handler.
    /// </summary>
    public string Type { get; init; }

    /// <summary>Gets the UTC timestamp when this event occurred.</summary>
    public DateTimeOffset Time { get; init; }

    /// <summary>
    /// Gets the media type of <see cref="Data"/> (e.g., <c>"application/json"</c>).
    /// </summary>
    public string? DataContentType { get; init; }

    /// <summary>
    /// Gets an optional subject that provides additional context
    /// about the subject of the event (e.g., an aggregate ID).
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets the event payload. The concrete type depends on the event <see cref="Type"/>;
    /// deserialize using <see cref="DataContentType"/> and the AsyncAPI schema registry.
    /// </summary>
    public object? Data { get; init; }
}
