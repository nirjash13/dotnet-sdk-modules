using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SaasBuilder.Host.Tenancy.Resolution;
using SaasBuilder.SharedKernel.Tenancy;
using SaasBuilder.SharedKernel.Tenancy.Resolution;
using Xunit;

namespace SaasBuilder.IntegrationTests.Phase3.Tenancy;

/// <summary>
/// Load-bearing tests for the tenant resolver pipeline priority ordering.
/// Tests the built-in resolver implementations directly (no WebApplicationFactory needed —
/// the resolvers are simple functions over HttpContext; end-to-end middleware tests are
/// in <see cref="AnonymousBypassTests"/>).
/// </summary>
public sealed class TenantResolverPipelineTests
{
    private static readonly Guid _tenantA = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid _tenantB = new Guid("bbbbbbbb-0000-0000-0000-000000000002");

    // ── Test 1: JWT claim wins when both JWT claim and header are present ────────
    [Fact]
    public async Task JwtResolver_WhenBothJwtAndHeaderPresent_JwtClaimWins()
    {
        // Arrange — JWT claim carries TenantA, header carries TenantB.
        DefaultHttpContext context = BuildJwtContext(_tenantA);
        context.Request.Headers["X-Tenant-Id"] = _tenantB.ToString();

        var jwtResolver = new JwtClaimTenantResolver();
        var headerResolver = new HeaderTenantResolver();

        // Act — evaluate in priority order (JWT first at 100, header at 50).
        Guid? fromJwt = await jwtResolver.ResolveAsync(context, CancellationToken.None);
        Guid? fromHeader = await headerResolver.ResolveAsync(context, CancellationToken.None);

        // Assert — JWT resolver returns TenantA; header resolver returns null for authenticated
        // end-users (not service accounts) as per the security invariant in HeaderTenantResolver.
        fromJwt.Should().Be(_tenantA);
        fromHeader.Should().BeNull(
            because: "HeaderTenantResolver rejects headers when the principal is authenticated " +
                     "and not a service account — this prevents JWT-authenticated users from " +
                     "substituting a different tenant via the header.");
    }

    // ── Test 2: Header resolver wins when no JWT claim ───────────────────────────
    [Fact]
    public async Task HeaderResolver_WhenNoJwtClaim_ReturnsHeaderTenant()
    {
        // Arrange — unauthenticated request with header.
        DefaultHttpContext context = BuildUnauthenticatedContext();
        context.Request.Headers["X-Tenant-Id"] = _tenantA.ToString();

        var jwtResolver = new JwtClaimTenantResolver();
        var headerResolver = new HeaderTenantResolver();

        // Act
        Guid? fromJwt = await jwtResolver.ResolveAsync(context, CancellationToken.None);
        Guid? fromHeader = await headerResolver.ResolveAsync(context, CancellationToken.None);

        // Assert
        fromJwt.Should().BeNull(because: "no tenant_id claim in the JWT");
        fromHeader.Should().Be(_tenantA);
    }

    // ── Test 3: Path resolver extracts tenant from /api/{tenantId}/resource ──────
    [Fact]
    public async Task PathResolver_WhenTenantIdInPath_ReturnsTenantFromPath()
    {
        // Arrange
        DefaultHttpContext context = BuildUnauthenticatedContext();
        context.Request.Path = $"/api/{_tenantA}/accounts";

        var pathResolver = new PathTenantResolver();

        // Act
        Guid? resolved = await pathResolver.ResolveAsync(context, CancellationToken.None);

        // Assert
        resolved.Should().Be(_tenantA);
    }

    // ── Test 4: Path resolver returns null when path does not match /api/ pattern ─
    [Fact]
    public async Task PathResolver_WhenPathDoesNotStartWithApi_ReturnsNull()
    {
        // Arrange — path starts with something else, not /api/.
        DefaultHttpContext context = BuildUnauthenticatedContext();
        context.Request.Path = "/connect/token";

        var pathResolver = new PathTenantResolver();

        // Act
        Guid? resolved = await pathResolver.ResolveAsync(context, CancellationToken.None);

        // Assert
        resolved.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static DefaultHttpContext BuildJwtContext(Guid tenantId)
    {
        // Build a ClaimsPrincipal with the tenant_id claim to simulate an authenticated JWT user.
        var identity = new System.Security.Claims.ClaimsIdentity(
            new[]
            {
                new System.Security.Claims.Claim("tenant_id", tenantId.ToString()),
                new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString()),
            },
            authenticationType: "Bearer");

        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        return context;
    }

    private static DefaultHttpContext BuildUnauthenticatedContext()
    {
        // No authentication — represents a service account or dev request.
        return new DefaultHttpContext();
    }
}
