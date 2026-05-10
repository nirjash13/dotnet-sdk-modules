using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SaasBuilder.SharedKernel.Tenancy.Resolution;

namespace SaasBuilder.Host.Tenancy.Resolution;

/// <summary>
/// Stub resolver that will resolve the tenant from an API key presented in the
/// <c>X-Api-Key</c> header or <c>Authorization: ApiKey {key}</c> scheme.
/// </summary>
/// <remarks>
/// TODO(Phase 2.9): Implement ApiKey tenant resolution. This requires the API key
/// infrastructure (hashed key store, key-to-tenant mapping) from Phase 2.9.
/// See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 2.9 (API keys and M2M tokens).
///
/// This stub is registered in the pipeline but throws <see cref="NotSupportedException"/>
/// on first dispatch if it would actually need to be used. Callers with a JWT or header
/// will never reach this resolver (lower priority).
/// </remarks>
internal sealed class ApiKeyTenantResolver : ITenantResolver
{
    private const string ApiKeyHeader = "X-Api-Key";

    /// <inheritdoc />
    /// <remarks>Priority 10 — lowest; API key resolution requires Phase 2.9 infrastructure.</remarks>
    public int Priority => 10;

    /// <inheritdoc />
    public ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken ct = default)
    {
        // Only activate if the header is actually present to avoid noise on every request.
        if (!context.Request.Headers.ContainsKey(ApiKeyHeader))
        {
            return new ValueTask<Guid?>((Guid?)null);
        }

        // Header is present but we cannot resolve it yet — throw so the caller knows this
        // capability exists in the interface but is not implemented.
        throw new NotSupportedException(
            "TODO(Phase 2.9): ApiKey tenant resolution is deferred. " +
            "See docs/SAAS_SDK_IMPLEMENTATION_PLAN.md Phase 2.9.");
    }
}
