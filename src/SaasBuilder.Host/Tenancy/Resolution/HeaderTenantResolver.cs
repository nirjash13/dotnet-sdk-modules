using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SaasBuilder.SharedKernel.Tenancy.Resolution;

namespace SaasBuilder.Host.Tenancy.Resolution;

/// <summary>
/// Resolves the tenant from the <c>X-Tenant-Id</c> HTTP request header.
/// Used for service accounts and dev/test scenarios where a full JWT is not available.
/// </summary>
/// <remarks>
/// Security note: this resolver is accepted only when the principal is not authenticated
/// OR the principal carries the <c>service-account</c> role. An authenticated end-user JWT
/// that also includes the header would have been resolved by <see cref="JwtClaimTenantResolver"/>
/// first (higher priority), so this resolver would not be reached for authenticated end-users.
/// The security invariant is maintained by the resolver priority ordering, not by this class.
/// </remarks>
internal sealed class HeaderTenantResolver : ITenantResolver
{
    private const string HeaderName = "X-Tenant-Id";

    /// <inheritdoc />
    /// <remarks>Priority 50 — lower than JWT (100), used for service accounts and dev.</remarks>
    public int Priority => 50;

    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        // Only accept the header when not authenticated OR when authenticated as a service account.
        // End-user JWTs always carry the tenant_id claim (resolved by JwtClaimTenantResolver first).
        bool principalIsAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        bool principalIsServiceAccount = context.User?.IsInRole("service-account") == true;

        if (principalIsAuthenticated && !principalIsServiceAccount)
        {
            return new ValueTask<Guid?>((Guid?)null);
        }

        string? headerValue = context.Request.Headers[HeaderName].ToString();

        if (!string.IsNullOrEmpty(headerValue) && Guid.TryParse(headerValue, out Guid tenantId))
        {
            return new ValueTask<Guid?>((Guid?)tenantId);
        }

        return new ValueTask<Guid?>((Guid?)null);
    }
}
