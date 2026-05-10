using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SaasBuilder.SharedKernel.Tenancy.Resolution;

namespace SaasBuilder.Host.Tenancy.Resolution;

/// <summary>
/// Resolves the tenant from the <c>tenant_id</c> JWT claim on the authenticated principal.
/// This is the highest-priority resolver (100) because the JWT is the authoritative identity source.
/// </summary>
/// <remarks>
/// The JWT principal is available only after <c>UseAuthentication()</c> has run.
/// <c>TenantMiddleware</c> runs after authentication, so the principal is always populated here.
/// An authenticated user who presents a JWT without a <c>tenant_id</c> claim returns null
/// (not an error) — the pipeline falls through to lower-priority resolvers.
/// </remarks>
internal sealed class JwtClaimTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    /// <remarks>Priority 100 — highest; JWT claim is the most authoritative source.</remarks>
    public int Priority => 100;

    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        string? claimValue = context.User?.FindFirst(TenantClaims.TenantId)?.Value;

        if (!string.IsNullOrEmpty(claimValue) && Guid.TryParse(claimValue, out Guid tenantId))
        {
            return new ValueTask<Guid?>((Guid?)tenantId);
        }

        return new ValueTask<Guid?>((Guid?)null);
    }
}
