using System;
using System.Collections.Generic;
using System.Globalization;
using CloudNative.CloudEvents;
using Chassis.SharedKernel.Contracts;

namespace Integration.CloudEventsAdapter;

/// <summary>
/// A static helper that maps a <see cref="CloudEvent"/> to MassTransit-compatible
/// message header pairs.
/// </summary>
/// <remarks>
/// <para>
/// This class has zero MassTransit dependency — it produces plain string-keyed header
/// pairs that callers attach to a MassTransit <c>SendContext</c> or <c>PublishContext</c>.
/// This keeps the CloudEventsAdapter library free of the MT version constraint and usable
/// from any transport layer (MassTransit, RabbitMQ.Client direct, HTTP, etc.).
/// </para>
/// <para>
/// Header names follow the CloudEvents AMQP protocol binding §3.1 and the
/// W3C Trace Context specification:
/// <list type="bullet">
///   <item><c>ce-id</c> — CloudEvents <c>id</c></item>
///   <item><c>ce-source</c> — CloudEvents <c>source</c> (URI string)</item>
///   <item><c>ce-type</c> — CloudEvents <c>type</c></item>
///   <item><c>ce-time</c> — CloudEvents <c>time</c> (RFC 3339)</item>
///   <item><c>traceparent</c> — W3C trace context propagation header</item>
/// </list>
/// </para>
/// </remarks>
public static class CloudEventsMassTransitBridge
{
    /// <summary>
    /// Produces MassTransit-compatible header key-value pairs from a <see cref="CloudEvent"/>.
    /// Attach these to a MassTransit <c>SendContext.Headers</c> or
    /// <c>PublishContext.Headers</c> using <c>Set(key, value)</c>.
    /// </summary>
    /// <param name="cloudEvent">
    /// The <see cref="CloudEvent"/> whose attributes should be propagated as headers.
    /// </param>
    /// <returns>
    /// An <see cref="IReadOnlyList{T}"/> of (header-name, header-value) pairs.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="cloudEvent"/> is <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<KeyValuePair<string, string>> ToPublishHeaders(
        CloudEvent cloudEvent)
    {
        if (cloudEvent == null)
        {
            throw new ArgumentNullException(nameof(cloudEvent));
        }

        var headers = new List<KeyValuePair<string, string>>(5);

        if (cloudEvent.Id != null)
        {
            headers.Add(new KeyValuePair<string, string>(
                CorrelationHeaders.CorrelationId,
                cloudEvent.Id));

            headers.Add(new KeyValuePair<string, string>("ce-id", cloudEvent.Id));
        }

        if (cloudEvent.Source != null)
        {
            headers.Add(new KeyValuePair<string, string>(
                "ce-source",
                cloudEvent.Source.ToString()));
        }

        if (cloudEvent.Type != null)
        {
            headers.Add(new KeyValuePair<string, string>("ce-type", cloudEvent.Type));
        }

        if (cloudEvent.Time.HasValue)
        {
            // RFC 3339 / ISO 8601 with offset — universally parseable.
            headers.Add(new KeyValuePair<string, string>(
                "ce-time",
                cloudEvent.Time.Value.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture)));
        }

        // traceparent placeholder — populated by the caller if OTel context is available.
        // Including the key with empty value signals that the header slot is reserved.
        headers.Add(new KeyValuePair<string, string>(
            CorrelationHeaders.TraceParent,
            string.Empty));

        return headers.AsReadOnly();
    }
}
