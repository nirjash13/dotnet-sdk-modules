using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using SaasBuilder.IntegrationTests.Phase2;
using Xunit;

namespace SaasBuilder.IntegrationTests.Phase3.Tenancy;

/// <summary>
/// Load-bearing tests asserting that infrastructure paths bypass tenant enforcement.
/// Uses the shared <see cref="SaasBuilderHostFixture"/> (real host + Testcontainers Postgres).
/// </summary>
[Collection("SaasBuilderHostFixture")]
public sealed class AnonymousBypassTests : IClassFixture<SaasBuilderHostFixture>
{
    private readonly SaasBuilderHostFixture _fixture;

    public AnonymousBypassTests(SaasBuilderHostFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    // ── Test: /health responds 200 with no tenant header ─────────────────────────
    [Fact]
    public async Task HealthEndpoint_WithNoTenantHeader_Returns200()
    {
        // Arrange — plain client with no JWT and no X-Tenant-Id header.
        using HttpClient client = _fixture.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");

        // Assert — /health is in the anonymous bypass list; no 401.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
