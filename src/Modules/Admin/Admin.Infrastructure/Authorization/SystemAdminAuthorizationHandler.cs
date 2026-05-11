using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Admin.Infrastructure.Authorization;

/// <summary>
/// Requirement that the current user is an authenticated system-admin who completed MFA.
/// </summary>
public sealed class SystemAdminRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Handler for <see cref="SystemAdminRequirement"/>.
/// Succeeds only when the principal has both <c>role=system-admin</c>
/// and <c>mfa_used=true</c> claims.
/// </summary>
public sealed class SystemAdminAuthorizationHandler
    : AuthorizationHandler<SystemAdminRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SystemAdminRequirement requirement)
    {
        bool hasRole = context.User.HasClaim("roles", "system-admin");
        bool hasMfa = context.User.HasClaim("mfa_used", "true");

        if (hasRole && hasMfa)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
