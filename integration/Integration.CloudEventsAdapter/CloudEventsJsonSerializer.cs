using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Integration.CloudEventsAdapter;

/// <summary>
/// Serializes and deserializes <see cref="CloudEvent"/> instances using the
/// CloudEvents 1.0 JSON Event Format (binary content mode convention).
/// Wraps <see cref="JsonEventFormatter"/> from <c>CloudNative.CloudEvents.SystemTextJson</c>.
/// </summary>
/// <remarks>
/// The "binary content mode" here refers to the CloudEvents specification convention
/// where the <c>data</c> field carries the JSON-serialized payload inline —
/// not to binary encoding. The formatter handles both structured and binary modes;
/// this wrapper always uses the structured JSON format for maximum interoperability.
/// </remarks>
public sealed class CloudEventsJsonSerializer
{
    private readonly JsonEventFormatter _formatter;

    /// <summary>
    /// Initializes a new instance of <see cref="CloudEventsJsonSerializer"/>
    /// with default <see cref="JsonEventFormatter"/> settings.
    /// </summary>
    public CloudEventsJsonSerializer()
    {
        _formatter = new JsonEventFormatter();
    }

    /// <summary>
    /// Serializes a <see cref="CloudEvent"/> to <paramref name="stream"/> using the
    /// CloudEvents 1.0 JSON structured content mode.
    /// </summary>
    /// <param name="stream">The target stream. Must be writable.</param>
    /// <param name="cloudEvent">The event to serialize.</param>
    /// <param name="cancellationToken">Propagated to underlying I/O.</param>
    public async Task SerializeAsync(
        Stream stream,
        CloudEvent cloudEvent,
        CancellationToken cancellationToken)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (cloudEvent == null)
        {
            throw new ArgumentNullException(nameof(cloudEvent));
        }

        var bytes = _formatter.EncodeStructuredModeMessage(cloudEvent, out _);

#if NET10_0_OR_GREATER
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
#else
        // netstandard2.0: WriteAsync(ReadOnlyMemory<byte>, CancellationToken) is not available.
        await stream.WriteAsync(bytes.ToArray(), 0, bytes.Length, cancellationToken).ConfigureAwait(false);
#endif
    }

    /// <summary>
    /// Deserializes a <see cref="CloudEvent"/> from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The source stream. Must be readable and positioned at the start.</param>
    /// <param name="cancellationToken">Propagated to underlying I/O.</param>
    /// <returns>The deserialized <see cref="CloudEvent"/>.</returns>
    public async Task<CloudEvent> DeserializeAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var ms = new MemoryStream();
#if NET10_0_OR_GREATER
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
#else
        await stream.CopyToAsync(ms).ConfigureAwait(false);
#endif
        var bytes = ms.ToArray();
        return _formatter.DecodeStructuredModeMessage(
            bytes,
            contentType: null,
            extensionAttributes: null);
    }
}
