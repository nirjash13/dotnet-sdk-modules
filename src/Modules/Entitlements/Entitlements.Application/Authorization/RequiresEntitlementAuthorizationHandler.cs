using System.Threading.Tasks;
using Entitlements.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using SaasBuilder.SharedKernel.Tenancy;

namespace Entitlements.Application.Authorization;

/// <summary>
/// ASP.NET Core authorization handler that checks whether the current tenant holds
/// a <see cref="RequiresEntitlementAttribute"/> entitlement.
///
/// On failure: sets 402 Payment Required via <see cref="HttpContext.Response.StatusCode"/>.
/// The global exception middleware is NOT involved here — we use the authorization pipeline.
/// </summary>
public sealed class RequiresEntitlementAuthorizationHandler(
    IEntitlementService entitlements,
    ITenantContextAccessor tenantAccessor)
    : AuthorizationHandler<RequiresEntitlementAttribute>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequiresEntitlementAttribute requirement)
    {
        if (tenantAccessor.Current is null)
        {
            context.Fail(new AuthorizationFailureReason(
                this, "No tenant context — cannot evaluate entitlement."));
            return;
        }

        if (requirement.AsLimit)
        {
            // TODO(Phase 4 — usage counter integration): Retrieve current usage count
            // for the resource type and compare against GetLimitAsync. For now, throw
            // to make it clear this path is not implemented so callers notice early.
            throw new System.NotImplementedException(
                "TODO(Phase 4): AsLimit=true entitlement checking requires a usage counter integration. " +
                "Implement IUsageMeter and wire it here.");
        }

        bool hasEntitlement = await entitlements
            .HasAsync(requirement.Key)
            .ConfigureAwait(false);

        if (hasEntitlement)
        {
            context.Succeed(requirement);
            return;
        }

        // Do not call context.Fail() — let ASP.NET Core return 403 by default.
        // The caller is responsible for mapping entitlement failures to 402 via a
        // custom IAuthorizationMiddlewareResultHandler registered in DI.
        // TODO(Phase 4): Register an IAuthorizationMiddlewareResultHandler that returns
        // 402 with code "entitlement.required" when RequiresEntitlementAttribute fails.
        context.Fail(new AuthorizationFailureReason(
            this, $"Entitlement '{requirement.Key}' is not granted for this tenant."));
    }
}
