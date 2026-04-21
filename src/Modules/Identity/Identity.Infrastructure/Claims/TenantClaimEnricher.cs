using System;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Chassis.SharedKernel.Tenancy;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Identity.Infrastructure.Claims;

/// <summary>
/// Infrastructure implementation of <see cref="ITenantClaimEnricher"/>.
/// Injects <c>tenant_id</c>, <c>roles</c>, and <c>membership_id</c> into the issued JWT
/// by mutating the principal inside the OpenIddict <c>ProcessSignInContext</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant claim source (Phase 2 §13 Q2 — Option A for user grants):</b>
/// The user's <em>primary</em> <see cref="UserTenantMembership"/> (IsPrimary = true) is the
/// authoritative source of tenant identity in the token. If the user has no primary membership,
/// sign-in fails with an <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// <b>Client-credentials tenant source (Option B):</b>
/// For client_credentials grants, tenant_id is read from the OpenIddict application's
/// <c>Properties["tenant_id"]</c> dictionary entry set at client registration time. This
/// binds tenant identity to the registered OAuth application rather than requiring a user context.
/// </para>
/// </remarks>
public sealed class TenantClaimEnricher(
    ITenantContextAccessor tenantContextAccessor,
    IUserRepository userRepository,
    IOpenIddictApplicationManager applicationManager,
    ILogger<TenantClaimEnricher> logger)
    : ITenantClaimEnricher
{
    /// <inheritdoc />
    public async Task EnrichAsync(object context, CancellationToken cancellationToken = default)
    {
        // ProcessSignInContext is a concrete type in OpenIddict.Server; accept as object
        // so the Application layer interface stays decoupled from OpenIddict types.
        if (context is not OpenIddict.Server.OpenIddictServerEvents.ProcessSignInContext signInContext)
        {
            return;
        }

        ClaimsPrincipal? principal = signInContext.Principal;
        if (principal is null)
        {
            return;
        }

        string? grantType = signInContext.Request?.GrantType;

        if (string.Equals(grantType, "client_credentials", StringComparison.OrdinalIgnoreCase))
        {
            // For client_credentials, derive tenant_id from the application's Properties bag.
            // This avoids a dependency on user context which does not exist in this flow.
            await EnrichClientCredentialsAsync(signInContext, principal, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // For authorization_code / refresh_token: resolve from the user's primary membership.
        // Open a bypass scope so the repository query runs without requiring a tenant context
        // in the interceptor and global query filter (this handler fires during token issuance,
        // before TenantMiddleware has established a context).
        using IDisposable bypass = tenantContextAccessor.BeginBypass();
        await EnrichUserGrantAsync(principal, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnrichClientCredentialsAsync(
        OpenIddict.Server.OpenIddictServerEvents.ProcessSignInContext signInContext,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        string? clientId = signInContext.Request?.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("TenantClaimEnricher: client_credentials request has no client_id; skipping enrichment.");
            return;
        }

        object? application = await applicationManager
            .FindByClientIdAsync(clientId, cancellationToken)
            .ConfigureAwait(false);

        if (application is null)
        {
            logger.LogWarning(
                "TenantClaimEnricher: client_credentials — application '{ClientId}' not found; skipping enrichment.",
                clientId);
            return;
        }

        // Read tenant_id from the application's Properties bag (set at registration time).
        // OpenIddict stores properties as ImmutableDictionary<string, JsonElement>.
        ImmutableDictionary<string, JsonElement> properties =
            await applicationManager.GetPropertiesAsync(application, cancellationToken).ConfigureAwait(false);

        if (!properties.TryGetValue("tenant_id", out JsonElement tenantIdElement)
            || !Guid.TryParse(tenantIdElement.GetString(), out Guid tenantId))
        {
            logger.LogWarning(
                "TenantClaimEnricher: application '{ClientId}' has no valid tenant_id property; " +
                "client_credentials token will not carry tenant_id.",
                clientId);
            return;
        }

        principal.SetClaim("tenant_id", tenantId.ToString());
        principal.SetClaim("tid", tenantId.ToString("N"));

        // Service accounts receive the service-account role so TenantMiddleware
        // will accept the X-Tenant-Id header for subsequent requests (Major #9).
        principal.AddClaim("roles", "service-account");

        logger.LogDebug(
            "TenantClaimEnricher: client_credentials enriched — client={ClientId} tenant={TenantId}",
            clientId,
            tenantId);
    }

    private async Task EnrichUserGrantAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        // Resolve user from the principal's 'sub' claim.
        string? subjectClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(subjectClaim) || !Guid.TryParse(subjectClaim, out Guid userId))
        {
            logger.LogWarning("TenantClaimEnricher: no parseable 'sub' claim on principal; skipping enrichment.");
            return;
        }

        // Resolve primary membership — authoritative source for tenant context in the token.
        // §13 Q2 decision: throw to reject sign-in rather than issuing a tenant-less token.
        UserTenantMembership membership = await userRepository
            .FindPrimaryMembershipAsync(userId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"User '{userId}' has no primary tenant membership. " +
                "Assign a primary membership before the user can sign in.");

        Guid tenantId = membership.TenantId;

        principal.SetClaim("tenant_id", tenantId.ToString());
        principal.SetClaim("tid", tenantId.ToString("N"));
        principal.SetClaim("membership_id", membership.Id.ToString());

        // Emit roles under the short claim name "roles" so that the JWT Bearer consumer
        // can read them via RoleClaimType = "roles" (matching TenantClaims.Roles).
        foreach (string role in membership.Roles)
        {
            principal.AddClaim("roles", role);
        }

        logger.LogDebug(
            "TenantClaimEnricher: enriched — user={UserId} tenant={TenantId} membership={MembershipId}",
            userId,
            tenantId,
            membership.Id);
    }
}
