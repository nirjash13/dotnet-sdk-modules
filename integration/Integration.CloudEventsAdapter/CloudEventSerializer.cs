using System;
using CloudNative.CloudEvents;
using Chassis.SharedKernel.Contracts;

namespace Integration.CloudEventsAdapter;

/// <summary>
/// Converts between strongly-typed integration event objects and
/// <see cref="CloudEvent"/> envelopes (CloudEvents 1.0).
/// </summary>
/// <remarks>
/// Zero dependency on MassTransit — this is a pure envelope conversion library.
/// No <c>Activator.CreateInstance(string)</c> or <c>Expression.Compile()</c> calls;
/// the only generic constraints used are interface-based, keeping this AOT-friendly.
/// </remarks>
public static class CloudEventSerializer
{
    private const string DefaultDataContentType = "application/json";

    /// <summary>
    /// Wraps a strongly-typed integration event in a <see cref="CloudEvent"/> envelope.
    /// </summary>
    /// <typeparam name="TIntegrationEvent">The concrete event type.</typeparam>
    /// <param name="message">The event payload to wrap.</param>
    /// <param name="source">
    /// The URI identifying the event producer (e.g., <c>new Uri("/chassis/ledger", UriKind.Relative)</c>).
    /// </param>
    /// <param name="type">
    /// The fully-qualified event type name
    /// (e.g., <c>"com.chassis.ledger.transaction.posted.v1"</c>).
    /// </param>
    /// <returns>A populated <see cref="CloudEvent"/> ready for serialization.</returns>
    public static CloudEvent ToCloudEvent<TIntegrationEvent>(
        TIntegrationEvent message,
        Uri source,
        string type)
        where TIntegrationEvent : class
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        var cloudEvent = new CloudEvent
        {
            Id = Guid.NewGuid().ToString("D"),
            Source = source,
            Type = type,
            Time = DateTimeOffset.UtcNow,
            DataContentType = DefaultDataContentType,
            Data = message,
        };

        return cloudEvent;
    }

    /// <summary>
    /// Extracts the typed payload from a <see cref="CloudEvent"/> envelope.
    /// </summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <param name="cloudEvent">The envelope to unwrap.</param>
    /// <returns>
    /// The <see cref="CloudEvent.Data"/> cast to <typeparamref name="T"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="cloudEvent"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="CloudEvent.Data"/> cannot be cast to <typeparamref name="T"/>.
    /// </exception>
    public static T FromCloudEvent<T>(CloudEvent cloudEvent)
        where T : class
    {
        if (cloudEvent == null)
        {
            throw new ArgumentNullException(nameof(cloudEvent));
        }

        var data = cloudEvent.Data;
        if (data is T typed)
        {
            return typed;
        }

        throw new InvalidOperationException(
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "CloudEvent data is of type '{0}' but expected '{1}'.",
                data?.GetType().FullName ?? "null",
                typeof(T).FullName));
    }

    /// <summary>
    /// Maps a <see cref="CloudEventEnvelope"/> (SharedKernel) to a <see cref="CloudEvent"/>.
    /// Used by the Framework 4.8 bridge to convert legacy SOAP messages that have already
    /// been mapped to a <see cref="CloudEventEnvelope"/>.
    /// </summary>
    /// <param name="envelope">The SharedKernel envelope.</param>
    /// <returns>A <see cref="CloudEvent"/> equivalent.</returns>
    public static CloudEvent FromEnvelope(CloudEventEnvelope envelope)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        var cloudEvent = new CloudEvent
        {
            Id = envelope.Id,
            Source = new Uri(envelope.Source, UriKind.RelativeOrAbsolute),
            Type = envelope.Type,
            Time = envelope.Time,
            DataContentType = envelope.DataContentType ?? DefaultDataContentType,
            Subject = envelope.Subject,
            Data = envelope.Data,
        };

        return cloudEvent;
    }
}
