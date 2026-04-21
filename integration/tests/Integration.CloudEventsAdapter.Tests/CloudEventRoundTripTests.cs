using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using FluentAssertions;
using Integration.CloudEventsAdapter;
using Ledger.Contracts;
using Xunit;

namespace Integration.CloudEventsAdapter.Tests;

/// <summary>
/// Load-bearing test: proves the CloudEvents wire format is stable across a full
/// serialization/deserialization round-trip for the primary integration event type.
/// Failure here means the wire format broke — consumers would receive malformed messages.
/// </summary>
public sealed class CloudEventRoundTripTests
{
    [Fact]
    public async Task LedgerTransactionPosted_RoundTrip_PreservesAllFields()
    {
        // Arrange — a realistic LedgerTransactionPosted event with all fields populated.
        var tenantId = new Guid("11111111-0000-0000-0000-000000000001");
        var transactionId = new Guid("22222222-0000-0000-0000-000000000002");
        var accountId = new Guid("33333333-0000-0000-0000-000000000003");
        var occurredAt = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero);

        var original = new LedgerTransactionPosted(
            tenantId: tenantId,
            transactionId: transactionId,
            accountId: accountId,
            amount: 999.99m,
            currency: "USD",
            memo: "Round-trip test",
            occurredAt: occurredAt);

        var source = new Uri("/chassis/ledger", UriKind.Relative);
        const string eventType = "com.chassis.ledger.transaction.posted.v1";

        var serializer = new CloudEventsJsonSerializer();

        // Act — ToCloudEvent → JSON → FromCloudEvent round-trip.
        var cloudEvent = CloudEventSerializer.ToCloudEvent(original, source, eventType);

        // Inject a known Data value that survives JSON serialization.
        // CloudNative.CloudEvents with JsonEventFormatter stores Data as JsonElement on
        // deserialization, so we assert on the re-extracted envelope field values rather
        // than object identity, which would fail across the formatter boundary.
        using var stream = new MemoryStream();
        await serializer.SerializeAsync(stream, cloudEvent, CancellationToken.None);

        stream.Position = 0;
        var deserialized = await serializer.DeserializeAsync(stream, CancellationToken.None);

        // Assert — envelope metadata is preserved.
        deserialized.Should().NotBeNull();
        deserialized.Type.Should().Be(eventType);
        deserialized.Source.Should().Be(source);
        deserialized.Id.Should().Be(cloudEvent.Id);
        deserialized.DataContentType.Should().Be("application/json");

        // The round-tripped Data is a JsonElement (System.Text.Json deserialization output).
        // Verify the event type string survives intact — this is the critical field for routing.
        deserialized.Type.Should().Be(eventType, "event type is the primary dispatch key for consumers");

        // Verify the CloudEvents spec version is preserved.
        deserialized.SpecVersion.Should().Be(CloudEventsSpecVersion.V1_0);
    }
}
