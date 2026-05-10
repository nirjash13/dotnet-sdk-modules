using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SaasBuilder.SharedKernel.Tenancy.Resolution;

namespace SaasBuilder.Host.Tenancy.Resolution;

/// <summary>
/// Resolves the tenant from the first path segment after <c>/api/</c>.
/// Supports URL patterns of the form <c>/api/{tenantId}/resource</c>.
/// </summary>
/// <remarks>
/// This resolver is intended for multi-tenant APIs where the tenant identifier is embedded
/// in the URL path. Use this pattern when building public-facing APIs where the tenant
/// is part of the resource address rather than an authentication concern.
/// Priority 30 — lower than JWT (100) and header (50).
/// </remarks>
internal sealed class PathTenantResolver : ITenantResolver
{
    private const string ApiPrefix = "/api/";

    /// <inheritdoc />
    /// <remarks>Priority 30 — lower than JWT (100) and header (50).</remarks>
    public int Priority => 30;

    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        string path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ValueTask<Guid?>((Guid?)null);
        }

        // Extract the segment after /api/
        ReadOnlySpan<char> afterPrefix = path.AsSpan(ApiPrefix.Length);
        int slashIndex = afterPrefix.IndexOf('/');
        ReadOnlySpan<char> segment = slashIndex >= 0
            ? afterPrefix[..slashIndex]
            : afterPrefix;

        if (segment.Length > 0 && Guid.TryParse(segment, out Guid tenantId))
        {
            return new ValueTask<Guid?>((Guid?)tenantId);
        }

        return new ValueTask<Guid?>((Guid?)null);
    }
}
