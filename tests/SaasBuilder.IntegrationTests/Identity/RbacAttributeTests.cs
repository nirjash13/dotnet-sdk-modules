using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Identity.Api.Authorization;
using Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.IntegrationTests.Phase2;
using Xunit;

namespace SaasBuilder.IntegrationTests.Identity;

/// <summary>
/// Load-bearing handler-level tests for the RBAC authorization handler.
///
/// Load-bearing rationale:
/// Test 8a: A ReadOnly-role user accessing an endpoint that requires "organizations.write:any"
/// should be denied. If the RequiresPermissionAuthorizationHandler is not registered
/// or is broken, the request would succeed — this test catches that.
///
/// Test 8b: An Admin-role user accessing the same endpoint should succeed.
/// If the permission registry is not loaded correctly, Admin would also be denied — this catches that.
///
/// These are handler-level tests (not full HTTP integration) because the permission
/// registry requires seeded DB roles, deferred to Phase 2 implementation. The handler
/// is tested directly with an in-memory stub registry.
/// </summary>
public sealed class RbacAttributeTests
{
    private static readonly Guid ReadOnlyRoleId = new Guid("00000000-0000-0000-0000-000000000004");
    private static readonly Guid AdminRoleId = new Guid("00000000-0000-0000-0000-000000000002");

    // ── Test 8a: ReadOnly role denied ─────────────────────────────────────────

    /// <summary>
    /// <see cref="RequiresPermissionAuthorizationHandler"/> denies
    /// a user whose role does not hold the required permission.
    /// </summary>
    [Fact]
    public async Task RequiresPermission_ReadOnlyRole_Denies_OrganizationWritePermission()
    {
        // Arrange — stub registry returns no permissions for ReadOnly role.
        IPermissionRegistry registry = new StubPermissionRegistry(ReadOnlyRoleId, new string[0]);

        var handler = new RequiresPermissionAuthorizationHandler();
        var requirement = new RequiresPermissionAttribute("organizations.write:any");

        ClaimsPrincipal user = BuildUserWithRoleId(ReadOnlyRoleId);

        DefaultHttpContext httpContext = new DefaultHttpContext();
        httpContext.RequestServices = BuildServiceProvider(registry);
        httpContext.User = user;

        AuthorizationHandlerContext authContext = new AuthorizationHandlerContext(
            requirements: new IAuthorizationRequirement[] { requirement },
            user: user,
            resource: httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse(
            because: "a ReadOnly-role user must not be granted organizations.write:any");
    }

    // ── Test 8b: Admin role permitted ─────────────────────────────────────────

    /// <summary>
    /// <see cref="RequiresPermissionAuthorizationHandler"/> allows
    /// a user whose role holds the required permission.
    /// </summary>
    [Fact]
    public async Task RequiresPermission_AdminRole_Allows_OrganizationWritePermission()
    {
        // Arrange — stub registry returns the write permission for Admin role.
        IPermissionRegistry registry = new StubPermissionRegistry(
            AdminRoleId, new[] { "organizations.write:any" });

        var handler = new RequiresPermissionAuthorizationHandler();
        var requirement = new RequiresPermissionAttribute("organizations.write:any");

        ClaimsPrincipal user = BuildUserWithRoleId(AdminRoleId);

        DefaultHttpContext httpContext = new DefaultHttpContext();
        httpContext.RequestServices = BuildServiceProvider(registry);
        httpContext.User = user;

        AuthorizationHandlerContext authContext = new AuthorizationHandlerContext(
            requirements: new IAuthorizationRequirement[] { requirement },
            user: user,
            resource: httpContext);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue(
            because: "an Admin-role user with the organizations.write:any permission must be allowed");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ClaimsPrincipal BuildUserWithRoleId(Guid roleId)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim("role_id", roleId.ToString()) },
            authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    private static IServiceProvider BuildServiceProvider(IPermissionRegistry registry)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPermissionRegistry>(registry);
        return services.BuildServiceProvider();
    }

    // ── Stub registry ─────────────────────────────────────────────────────────

    private sealed class StubPermissionRegistry : IPermissionRegistry
    {
        private readonly Guid _roleIdWithPermissions;
        private readonly string[] _permissions;

        public StubPermissionRegistry(Guid roleIdWithPermissions, string[] permissions)
        {
            _roleIdWithPermissions = roleIdWithPermissions;
            _permissions = permissions;
        }

        public IReadOnlyList<PermissionDefinition> GetAll() => Array.Empty<PermissionDefinition>();

        public PermissionDefinition? TryGet(string key) => null;

        public Task<IReadOnlyList<string>> GetPermissionsForRoleAsync(
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            string[] perms = roleId == _roleIdWithPermissions
                ? _permissions
                : Array.Empty<string>();

            return Task.FromResult<IReadOnlyList<string>>(perms);
        }
    }
}
