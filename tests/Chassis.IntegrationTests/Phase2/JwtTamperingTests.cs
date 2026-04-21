using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Chassis.IntegrationTests.Phase2;

/// <summary>
/// Acceptance criterion (c): tampered JWT (resigned/stripped/wrong aud/wrong iss) returns 401.
///
/// Load-bearing rationale: each test targets a distinct JWT attack vector. A bug in
/// <see cref="Chassis.Host.Configuration.AddChassisAuthenticationExtensions"/> that
/// accidentally disables one of the validation parameters (ValidateIssuerSigningKey,
/// ValidateAudience, ValidateIssuer, ValidateLifetime) would cause exactly that test
/// to stop failing — proving the test would catch the regression.
/// </summary>
[Collection("ChassisHostFixture")]
public sealed class JwtTamperingTests : IClassFixture<ChassisHostFixture>
{
    private readonly ChassisHostFixture _fixture;

    public JwtTamperingTests(ChassisHostFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    // ── Sanity baseline ──────────────────────────────────────────────────────────
    // Without this baseline a 401 from tampering tests is meaningless — the server
    // might be returning 401 for every request regardless of token validity.
    // Hits /api/v1/identity/me (protected by RequireAuthorization) to prove the host
    // accepts a structurally valid JWT and that the token subject is visible.
    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        // Arrange — mint a structurally valid token for Tenant A.
        string token = ChassisHostFixture.MintToken(
            tenantId: ChassisHostFixture.TenantA,
            userId: ChassisHostFixture.UserA);

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act — /api/v1/identity/me requires authentication and a tenant context.
        // A valid token with tenant_id should return 200 with principal details.
        HttpResponseMessage response = await client.GetAsync("/api/v1/identity/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a valid JWT with tenant_id should be accepted by the protected /me endpoint");
    }

    // ── Attack 1: signature with wrong key ────────────────────────────────────────
    [Fact]
    public async Task TokenSignedWithDifferentKey_Returns401()
    {
        // Arrange — generate a fresh RSA key unrelated to the test signing key.
        var foreignRsa = RSA.Create(2048);
        var foreignKey = new RsaSecurityKey(foreignRsa);

        string tamperedToken = ChassisHostFixture.MintToken(
            tenantId: ChassisHostFixture.TenantA,
            userId: ChassisHostFixture.UserA,
            signingKey: foreignKey);

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        // Act — hit /api/v1/identity/me which is behind RequireAuthorization.
        HttpResponseMessage response = await client.GetAsync("/api/v1/identity/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "a JWT signed with a key unknown to the server must be rejected");
    }

    // ── Attack 2: wrong audience ───────────────────────────────────────────────────
    [Fact]
    public async Task TokenWithWrongAudience_Returns401()
    {
        // Arrange — valid signature, wrong aud claim.
        string tamperedToken = ChassisHostFixture.MintToken(
            tenantId: ChassisHostFixture.TenantA,
            userId: ChassisHostFixture.UserA,
            audience: "not-chassis-api");

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/identity/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "a JWT with an audience mismatch must be rejected per ValidateAudience=true");
    }

    // ── Attack 3: wrong issuer ────────────────────────────────────────────────────
    [Fact]
    public async Task TokenWithWrongIssuer_Returns401()
    {
        // Arrange — valid signature, wrong iss claim.
        string tamperedToken = ChassisHostFixture.MintToken(
            tenantId: ChassisHostFixture.TenantA,
            userId: ChassisHostFixture.UserA,
            issuer: "https://evil-issuer.example.com");

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/identity/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "a JWT from an unexpected issuer must be rejected per ValidateIssuer=true");
    }

    // ── Attack 4: expired token ────────────────────────────────────────────────────
    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        // Arrange — token whose exp is one hour in the past (outside the 5-second ClockSkew).
        string expiredToken = ChassisHostFixture.MintToken(
            tenantId: ChassisHostFixture.TenantA,
            userId: ChassisHostFixture.UserA,
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1));

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/identity/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "an expired JWT must be rejected per ValidateLifetime=true");
    }

    // ── Attack 5: missing tenant_id claim ────────────────────────────────────────
    // Even if the JWT is cryptographically valid, TenantMiddleware must reject it when
    // the tenant_id claim is absent (MissingTenantException → 401 code=missing_tenant_claim).
    [Fact]
    public async Task ValidTokenWithNoTenantIdClaim_Returns401WithMissingTenantCode()
    {
        // Arrange — directly craft a JWT with only a sub claim (no tenant_id).
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim("sub", ChassisHostFixture.UserA.ToString()),
                    // Intentionally omit tenant_id claim.
                }),
            Issuer = ChassisHostFixture.TestIssuer,
            Audience = ChassisHostFixture.TestAudience,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                ChassisHostFixture.SigningKey,
                SecurityAlgorithms.RsaSha256),
        };

        string tokenWithoutTenant = handler.CreateEncodedJwt(tokenDescriptor);

        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenWithoutTenant);

        // Act — JwtBearer accepts the token; TenantMiddleware throws MissingTenantException
        // which ProblemDetailsExceptionHandler maps to 401 code=missing_tenant_claim.
        HttpResponseMessage response = await client.GetAsync("/api/v1/identity/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "TenantMiddleware must reject authenticated requests missing the tenant_id claim");

        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("missing_tenant_claim",
            because: "ProblemDetailsExceptionHandler maps MissingTenantException to code=missing_tenant_claim");
    }
}
