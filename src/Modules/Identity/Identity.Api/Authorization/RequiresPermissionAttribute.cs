using System;
using System.Threading.Tasks;
using Identity.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Api.Authorization;

/// <summary>
/// Declares that the decorated endpoint or controller requires a specific permission key.
/// Evaluated by <see cref="RequiresPermissionAuthorizationHandler"/>.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// endpoints.MapPost("/api/v1/organizations/{id}/members:invite", Handler)
///     .RequiresPermission("members.invite:any");
/// </code>
/// </remarks>
/// <param name="permissionKey">The canonical permission key in the form "{resource}.{action}:{scope}".</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresPermissionAttribute(string permissionKey)
    : Attribute, IAuthorizationRequirement
{
    /// <summary>Gets the permission key that must be held by the calling user's role.</summary>
    public string PermissionKey { get; } = permissionKey
        ?? throw new ArgumentNullException(nameof(permissionKey));
}

/// <summary>
/// ASP.NET Core authorization handler that evaluates <see cref="RequiresPermissionAttribute"/>.
/// Looks up the caller's role from JWT claims and checks it against the permission registry.
/// </summary>
public sealed class RequiresPermissionAuthorizationHandler
    : AuthorizationHandler<RequiresPermissionAttribute>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequiresPermissionAttribute requirement)
    {
        // Resolve registry from the HTTP context service provider if available.
        // Falls back to a deny if the registry is unavailable.
        if (context.Resource is not Microsoft.AspNetCore.Http.HttpContext httpContext)
        {
            context.Fail();
            return;
        }

        IPermissionRegistry? registry = httpContext.RequestServices
            .GetService<IPermissionRegistry>();

        if (registry is null)
        {
            context.Fail();
            return;
        }

        // Extract role id from JWT claim "role_id".
        // TODO(Phase 2 — implementation): replace string claim with structured role claim
        // once JWT enrichment (TenantClaimEnricher extension) emits role_id per org.
        System.Security.Claims.Claim? roleIdClaim = context.User.FindFirst("role_id");
        if (roleIdClaim is null || !Guid.TryParse(roleIdClaim.Value, out Guid roleId))
        {
            context.Fail();
            return;
        }

        System.Collections.Generic.IReadOnlyList<string> permissions =
            await registry.GetPermissionsForRoleAsync(roleId, httpContext.RequestAborted)
                .ConfigureAwait(false);

        if (permissions.Contains(requirement.PermissionKey, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
