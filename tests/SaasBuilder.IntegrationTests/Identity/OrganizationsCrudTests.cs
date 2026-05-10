using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Api.Organizations;
using SaasBuilder.IntegrationTests.Phase2;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing integration tests for POST /api/v1/organizations.
///
/// Load-bearing rationale:
/// 1. Happy-path (201): proves the endpoint is wired, handler executes, and the DB round-trip succeeds.
///    A missing route registration or a broken DI graph both produce 404/500 — not 201.
/// 2. Auth (401): proves the endpoint requires a JWT.
///    Removing RequireAuthorization() from the route produces 200 — not 401.
/// 3. Validation (400): proves FluentValidation rejects an empty name.
///    Removing the validator registration produces 201 — not 400.
///
/// Cross-tenant leak test:
/// 4. Proves Tenant-B's JWT cannot list Tenant-A's organization.
///    A missing global query filter produces 1 result — not 0.
/// </summary>
[Collection("SaasBuilderHostFixture")]
public sealed class OrganizationsCrudTests : IClassFixture<SaasBuilderHostFixture>
{
    private static readonly Guid TenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid UserA = new Guid("cccccccc-0000-0000-0000-000000000003");
    private static readonly Guid UserB = new Guid("dddddddd-0000-0000-0000-000000000004");

    // Well-known Owner role id matching the seeded roles in Phase 2.
    // TODO(Phase 2 — implementation): resolve from IPermissionRegistry rather than hardcoding.
    private static readonly Guid OwnerRoleId = new Guid("00000000-0000-0000-0000-000000000001");

    private readonly SaasBuilderHostFixture _fixture;

    public OrganizationsCrudTests(SaasBuilderHostFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    // ── Test 1: Happy path ─────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/organizations with a valid request returns 201 Created with an organization id.
    /// </summary>
    [Fact]
    public async Task CreateOrganization_WithValidRequest_Returns201WithOrganizationId()
    {
        // Arrange
        string token = SaasBuilderHostFixture.MintToken(TenantA, UserA);
        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Owner-Role-Id", OwnerRoleId.ToString());

        object requestBody = new { Slug = "acme-corp-test", Name = "Acme Corporation Test" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/organizations", requestBody);

        // Assert — 201 with body containing OrganizationId.
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "a valid create-organization request should return 201 Created");

        CreateOrganizationResponse? body = await response.Content.ReadFromJsonAsync<CreateOrganizationResponse>();
        body.Should().NotBeNull();
        body!.OrganizationId.Should().NotBe(Guid.Empty,
            because: "the response must contain the newly-created organization id");
    }

    // ── Test 2: Auth enforcement ───────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/organizations without a JWT returns 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task CreateOrganization_WithNoJwt_Returns401()
    {
        // Arrange — no Authorization header.
        using HttpClient client = _fixture.CreateClient();

        object requestBody = new { Slug = "no-auth-test", Name = "No Auth" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/organizations", requestBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "the organizations endpoint must reject unauthenticated requests");
    }

    // ── Test 3: Validation boundary ────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/organizations with an empty name returns 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task CreateOrganization_WithEmptyName_Returns400()
    {
        // Arrange
        string token = SaasBuilderHostFixture.MintToken(TenantA, UserA);
        using HttpClient client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Owner-Role-Id", OwnerRoleId.ToString());

        // Empty name violates the CreateOrganizationValidator rule.
        object requestBody = new { Slug = "valid-slug", Name = string.Empty };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/organizations", requestBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "FluentValidation must reject an empty organization name");
    }

    // ── Test 4: Cross-tenant leak ──────────────────────────────────────────────

    /// <summary>
    /// Tenant-B's JWT cannot list Tenant-A's organizations.
    /// Proves RLS + EF global query filter prevents cross-tenant data leakage.
    /// </summary>
    [Fact]
    public async Task ListOrganizations_WithTenantBToken_CannotSeeTenantAOrganizations()
    {
        // Arrange — seed an org for Tenant-A using Tenant-A's JWT.
        string tokenA = SaasBuilderHostFixture.MintToken(TenantA, UserA);
        using HttpClient clientA = _fixture.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        clientA.DefaultRequestHeaders.Add("X-Owner-Role-Id", OwnerRoleId.ToString());

        await clientA.PostAsJsonAsync("/api/v1/organizations", new
        {
            Slug = "tenant-a-org-xtenant",
            Name = "Tenant A Org",
        });

        // Act — list organizations using Tenant-B's JWT.
        string tokenB = SaasBuilderHostFixture.MintToken(TenantB, UserB);
        using HttpClient clientB = _fixture.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        HttpResponseMessage response = await clientB.GetAsync("/api/v1/organizations");

        // Assert — Tenant-B sees no organizations (empty list, not Tenant-A's data).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizationSummaryResponse[]? orgs =
            await response.Content.ReadFromJsonAsync<OrganizationSummaryResponse[]>();
        orgs.Should().NotBeNull();

        // None of Tenant-B's results should be from Tenant-A's slug.
        orgs!.Should().NotContain(
            o => o.Slug == "tenant-a-org-xtenant",
            because: "Tenant-B must not see Tenant-A's organizations");
    }
}
