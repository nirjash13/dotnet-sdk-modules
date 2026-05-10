using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SaasBuilder.SharedKernel.Tenancy.Resolution;

namespace SaasBuilder.Host.Tenancy.Resolution;

/// <summary>
/// Resolves the tenant from the first DNS label of the <c>Host</c> header.
/// Supports subdomain-per-tenant patterns of the form <c>{tenantSlug}.example.com</c>.
/// </summary>
/// <remarks>
/// <para>
/// This resolver extracts the first label from the Host header and attempts to parse it
/// as a GUID. It does not perform a slug-to-GUID lookup — callers are expected to structure
/// their subdomains as GUIDs (e.g., <c>aaaaaaaa-0000-0000-0000-000000000001.example.com</c>)
/// or to provide a higher-priority resolver that performs the slug lookup.
/// </para>
/// <para>
/// TODO(Phase 3.3): Extend to support slug-based subdomain resolution via a
/// <c>ITenantSlugResolver</c> lookup. See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 3.3.
/// </para>
/// Priority 20 — lowest of the built-in resolvers.
/// </remarks>
internal sealed class SubdomainTenantResolver : ITenantResolver
{
    /// <inheritdoc />
    /// <remarks>Priority 20 — lowest of the built-in resolvers.</remarks>
    public int Priority => 20;

    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        string? host = context.Request.Host.Host;

        if (string.IsNullOrEmpty(host))
        {
            return new ValueTask<Guid?>((Guid?)null);
        }

        // Extract the first DNS label (everything before the first dot).
        int dotIndex = host.IndexOf('.', StringComparison.Ordinal);
        string firstLabel = dotIndex > 0 ? host[..dotIndex] : host;

        if (!string.IsNullOrEmpty(firstLabel) && Guid.TryParse(firstLabel, out Guid tenantId))
        {
            return new ValueTask<Guid?>((Guid?)tenantId);
        }

        return new ValueTask<Guid?>((Guid?)null);
    }
}
