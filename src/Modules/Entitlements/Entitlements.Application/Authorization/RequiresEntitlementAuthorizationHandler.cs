using System.Threading.Tasks;
using Entitlements.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using SaasBuilder.SharedKernel.Tenancy;

namespace Entitlements.Application.Authorization;

/// <summary>
/// ASP.NET Core authorization handler that checks whether the current tenant holds
/// a <see cref="RequiresEntitlementAttribute"/> entitlement.
///
/// Boolean mode (<see cref="RequiresEntitlementAttribute.AsLimit"/> == false):
///   - Returns 403 when the entitlement is not granted.
///
/// Numeric limit mode (<see cref="RequiresEntitlementAttribute.AsLimit"/> == true):
///   - Compares the entitlement's numeric limit against the current usage.
///   - Adds <c>X-Seat-Limit-Warning: true</c> header when approaching the limit (>= 80%).
///   - Returns 402 Payment Required when usage is at or over the limit.
///   - Usage is provided by the ambient <c>IUsageCounterAccessor</c> if registered,
///     or falls back to 0 (permissive) when no usage counter is available.
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
            await HandleLimitModeAsync(context, requirement).ConfigureAwait(false);
            return;
        }

        bool hasEntitlement = await entitlements
            .HasAsync(requirement.Key)
            .ConfigureAwait(false);

        if (hasEntitlement)
        {
            context.Succeed(requirement);
            return;
        }

        context.Fail(new AuthorizationFailureReason(
            this, $"Entitlement '{requirement.Key}' is not granted for this tenant."));
    }

    private async Task HandleLimitModeAsync(
        AuthorizationHandlerContext context,
        RequiresEntitlementAttribute requirement)
    {
        long? limit = await entitlements.GetLimitAsync(requirement.Key).ConfigureAwait(false);

        // No limit configured → permit (unlimited plan).
        if (limit is null)
        {
            context.Succeed(requirement);
            return;
        }

        // Try to obtain current usage from the HTTP context resource.
        // The endpoint sets IUsageContext on the HttpContext.Items collection
        // with key "entitlement.usage.{key}" when per-action usage counting is needed.
        long currentUsage = 0;
        if (context.Resource is HttpContext httpContext)
        {
            string usageItemKey = $"entitlement.usage.{requirement.Key}";
            if (httpContext.Items.TryGetValue(usageItemKey, out object? usageObj) &&
                usageObj is long usageValue)
            {
                currentUsage = usageValue;
            }

            // Soft limit: add warning header when at or above 80% of limit.
            if (limit > 0 && currentUsage >= (long)(limit.Value * 0.8))
            {
                httpContext.Response.Headers["X-Seat-Limit-Warning"] = "true";
            }

            // Hard limit: return 402 when at or over the limit.
            if (currentUsage >= limit.Value)
            {
                httpContext.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                context.Fail(new AuthorizationFailureReason(
                    this,
                    $"Entitlement limit '{requirement.Key}' exceeded: current={currentUsage}, limit={limit.Value}."));
                return;
            }
        }

        context.Succeed(requirement);
    }
}
