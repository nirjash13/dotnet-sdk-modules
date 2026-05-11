using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using SaasBuilder.IntegrationTests.Phase2;
using Xunit;

namespace SaasBuilder.IntegrationTests.Admin;

/// <summary>
/// Verifies that Admin module endpoints enforce the SystemAdmin policy:
/// - 401 when unauthenticated
/// - 403 when authenticated but missing system-admin role
/// - 403 when authenticated with role but missing mfa_used=true claim
/// </summary>
[Collection("SaasBuilderHost")]
public sealed class AdminAuthorizationTests(SaasBuilderHostFixture fixture) : IClassFixture<SaasBuilderHostFixture>
{
    [Fact]
    public async Task ListTenants_WhenUnauthenticated_Returns401()
    {
        using HttpClient client = fixture.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListTenants_WhenAuthenticatedWithoutSystemAdminRole_Returns403()
    {
        using HttpClient client = fixture.CreateClient();

        // Mint a token with tenant-user role but NOT system-admin.
        string token = SaasBuilderHostFixture.MintToken(
            SaasBuilderHostFixture.TenantA,
            SaasBuilderHostFixture.UserA,
            roles: new[] { "member" });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListTenants_WhenSystemAdminRoleButMissingMfaClaim_Returns403()
    {
        using HttpClient client = fixture.CreateClient();

        // Mint a token with system-admin role but WITHOUT mfa_used=true claim.
        // MintToken only adds "roles" claims; mfa_used is NOT added.
        string token = SaasBuilderHostFixture.MintToken(
            SaasBuilderHostFixture.TenantA,
            SaasBuilderHostFixture.UserA,
            roles: new[] { "system-admin" });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/tenants");

        // SystemAdmin policy requires BOTH role AND mfa_used=true — missing MFA = 403.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
