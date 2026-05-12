using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace SaasBuilder.SecurityTests;

/// <summary>
/// S3 (C-22/C-27): Verifies that forged or arbitrary cookie bytes do not grant access
/// to protected backend endpoints. The backend must validate the Bearer token, not trust
/// cookie presence alone.
/// </summary>
/// <remarks>
/// Load-bearing: if the backend were to trust cookie presence without signature validation,
/// this test would return 200 instead of 401, catching the regression immediately.
/// </remarks>
[Collection("SaasBuilderSecurityFixture")]
public sealed class CookieTrustTests : IClassFixture<SaasBuilderSecurityFixture>
{
    private const string ProtectedEndpoint = "/api/v1/identity/me";

    private readonly SaasBuilderSecurityFixture _fixture;

    public CookieTrustTests(SaasBuilderSecurityFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ForgedCookieWithArbitraryBytes_HitsProtectedEndpoint_Returns401()
    {
        // Arrange — craft a cookie that looks like sb_token but contains garbage bytes.
        // If the backend trusts cookie presence over token validation this would return 200.
        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "sb_token=INVALID.GARBAGE.BYTES");

        // No Authorization header — no valid Bearer token.

        // Act
        HttpResponseMessage response = await client.GetAsync(ProtectedEndpoint);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "a forged cookie with arbitrary bytes must never grant access to a protected " +
                     "backend endpoint; the backend must enforce Bearer token validation, not cookie presence");
    }
}
