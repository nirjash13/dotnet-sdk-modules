using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Integration.AsyncApiRegistry.Tests;

/// <summary>
/// Load-bearing test: proves the AsyncAPI registry serves a valid, parseable 3.0 document
/// for the known Ledger schema. Failure here means the registry is broken and consumers
/// cannot discover event contracts at runtime.
/// </summary>
public sealed class AsyncApiSchemaEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetLedgerTransactionPostedSchema_Returns200WithAsyncApi30Document()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            "/schemas/ledger/transaction-posted/1.0.0",
            System.Threading.CancellationToken.None);

        // Assert — status code
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the ledger schema file exists in wwwroot");

        // Assert — response is parseable JSON with asyncapi: "3.0.0"
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace("schema file should have content");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.TryGetProperty("asyncapi", out var asyncapiProp).Should().BeTrue(
            "AsyncAPI 3.0 documents must have a top-level 'asyncapi' field");

        asyncapiProp.GetString().Should().Be(
            "3.0.0",
            "the schema file declares asyncapi: \"3.0.0\"");
    }
}
