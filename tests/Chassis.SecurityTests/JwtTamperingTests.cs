using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Chassis.SecurityTests;

/// <summary>
/// Validates all JWT attack vectors the chassis authentication must reject.
/// </summary>
/// <remarks>
/// Load-bearing rationale: each test targets a distinct validation parameter in
/// <c>AddChassisAuthentication</c>. Disabling any one parameter (e.g. ValidateIssuer=false)
/// would cause exactly that test to stop failing — proving the test will catch the regression.
/// The baseline test (vector 1) ensures 401s from tampered tokens are not caused by an
/// unrelated server fault that rejects all requests.
/// </remarks>
[Collection("ChassisSecurityFixture")]
public sealed class JwtTamperingTests : IClassFixture<ChassisSecurityFixture>
{
    private const string ProtectedEndpoint = "/api/v1/identity/me";

    private readonly ChassisSecurityFixture _fixture;

    public JwtTamperingTests(ChassisSecurityFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    // Vectors 2–6 use [Theory] where the setup is identical (tampered token → 401).
    // Vector 1 is a separate [Fact] because it asserts 200, not 401.

    // ── Vector 1: valid token → 200 (sanity baseline) ─────────────────────────────────

    [Fact]
    public async Task ValidToken_Returns200()
    {
        // Without this baseline, a 401 from any other test is meaningless —
        // the server might be returning 401 for every request regardless.
        string token = ChassisSecurityFixture.MintToken(
            tenantId: ChassisSecurityFixture.TenantA,
            userId: ChassisSecurityFixture.UserA);

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a valid JWT with tenant_id claim must be accepted by the protected /me endpoint");
    }

    // ── Vectors 2–6: tampered tokens must all produce 401 ─────────────────────────────

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-issuer")]
    [InlineData("expired")]
    [InlineData("missing-tenant-claim")]
    public async Task TamperedToken_Returns401(string scenario)
    {
        string tamperedToken = BuildTamperedToken(scenario);

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        HttpResponseMessage response = await client.GetAsync(ProtectedEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: $"scenario '{scenario}' represents a token attack that must be rejected with 401. " +
                     "A 200 or any non-401 response means a JWT validation parameter is not enforced.");

        if (scenario == "missing-tenant-claim")
        {
            // TenantMiddleware maps MissingTenantException → 401 code=missing_tenant_claim.
            string body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("missing_tenant_claim",
                because: "TenantMiddleware must surface the specific error code so clients can diagnose " +
                         "missing tenant_id claim vs. general auth failure");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────

    private static string BuildTamperedToken(string scenario)
    {
        return scenario switch
        {
            // Attack 2: JWT signed with a key the server does not recognise.
            "wrong-key" => ChassisSecurityFixture.MintToken(
                tenantId: ChassisSecurityFixture.TenantA,
                userId: ChassisSecurityFixture.UserA,
                signingKey: new RsaSecurityKey(RSA.Create(2048))),

            // Attack 3: wrong audience — valid signature, wrong aud claim.
            "wrong-audience" => ChassisSecurityFixture.MintToken(
                tenantId: ChassisSecurityFixture.TenantA,
                userId: ChassisSecurityFixture.UserA,
                audience: "not-chassis-api"),

            // Attack 4: wrong issuer — valid signature, wrong iss claim.
            "wrong-issuer" => ChassisSecurityFixture.MintToken(
                tenantId: ChassisSecurityFixture.TenantA,
                userId: ChassisSecurityFixture.UserA,
                issuer: "https://evil-issuer.example.com"),

            // Attack 5: expired token — exp one hour in the past, outside the 5s ClockSkew.
            "expired" => ChassisSecurityFixture.MintToken(
                tenantId: ChassisSecurityFixture.TenantA,
                userId: ChassisSecurityFixture.UserA,
                notBefore: DateTime.UtcNow.AddHours(-2),
                expires: DateTime.UtcNow.AddHours(-1)),

            // Attack 6: missing tenant_id claim — JwtBearer accepts it; TenantMiddleware rejects it.
            "missing-tenant-claim" => ChassisSecurityFixture.MintTokenWithoutTenantClaim(
                ChassisSecurityFixture.UserA),

            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown test scenario."),
        };
    }
}
