using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Chassis.SecurityTests;

/// <summary>
/// Verifies that the rate limiter applied to <c>/connect/*</c> auth endpoints rejects
/// requests that exceed the configured window limit.
/// </summary>
/// <remarks>
/// Load-bearing rationale: the rate limiter is the only defence against credential-stuffing
/// and token-endpoint brute-force attacks. If it were accidentally removed or its permit limit
/// set to zero (unlimited), this test would stop failing — proving it catches the regression.
/// </remarks>
[Collection("ChassisSecurityFixture")]
public sealed class RateLimitTests : IClassFixture<ChassisSecurityFixture>
{
    private readonly ChassisSecurityFixture _fixture;

    public RateLimitTests(ChassisSecurityFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    [Fact]
    public async Task ConnectToken_ExceedsWindowLimit_Returns429WithRfc7807Body()
    {
        // Load-bearing: proves the global rate limiter is wired to /connect/* and rejects
        // requests beyond the configured PermitLimit within the same window.
        //
        // We override the fixture's permit limit to 2 via config so the test completes in
        // milliseconds without relying on real-time window expiry. Each request sends an
        // intentionally invalid client_id form body — this is fine because we are testing
        // the rate limiter, not the token issuance logic. The rate limiter fires before
        // OpenIddict processes the request, so the 401/400 vs 429 distinction is unambiguous.
        const int permitLimit = 2;

        // Use the same client_id each time so the partitioned limiter uses the same bucket.
        var formContent = new FormUrlEncodedContent(
        [
            new("grant_type", "client_credentials"),
            new("client_id", "rate-limit-test-client"),
            new("client_secret", "invalid-secret"),
        ]);

        using HttpClient client = _fixture.CreateClientWithRateLimit(permitLimit);

        // Exhaust the window: send `permitLimit` requests (these may succeed or fail for
        // auth reasons — we only care that the rate limiter does not fire yet).
        for (int i = 0; i < permitLimit; i++)
        {
            using var perRequestContent = new FormUrlEncodedContent(
            [
                new("grant_type", "client_credentials"),
                new("client_id", "rate-limit-test-client"),
                new("client_secret", "invalid-secret"),
            ]);

            HttpResponseMessage warmUpResponse = await client.PostAsync("/connect/token", perRequestContent);

            // The response should NOT be 429 while within the limit window.
            warmUpResponse.StatusCode.Should().NotBe(
                HttpStatusCode.TooManyRequests,
                because: $"request {i + 1} of {permitLimit} should be within the rate-limit window");
        }

        // The (permitLimit + 1)th request must be rejected by the rate limiter.
        using var overLimitContent = new FormUrlEncodedContent(
        [
            new("grant_type", "client_credentials"),
            new("client_id", "rate-limit-test-client"),
            new("client_secret", "invalid-secret"),
        ]);

        HttpResponseMessage rejectedResponse = await client.PostAsync("/connect/token", overLimitContent);

        rejectedResponse.StatusCode.Should().Be(
            HttpStatusCode.TooManyRequests,
            because: $"the {permitLimit + 1}th request must be rejected by the fixed-window rate limiter. " +
                     "A non-429 response means the rate limiter is not enforced on /connect/token, " +
                     "leaving auth endpoints open to credential-stuffing attacks.");

        // The response body must be RFC 7807 ProblemDetails (configured in RateLimitingExtensions.OnRejected).
        string body = await rejectedResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Too Many Requests",
            because: "the rate-limit rejection response must include RFC 7807 ProblemDetails " +
                     "so clients and monitoring tools can distinguish rate-limit from auth failures");
    }
}
