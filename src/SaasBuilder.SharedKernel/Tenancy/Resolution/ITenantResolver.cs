using System;
using System.Threading;
using System.Threading.Tasks;

// HttpContext is only available on net10.0 (ASP.NET Core shared framework).
// The netstandard2.0 target omits this interface; Host-layer code targets net10.0.
#if NET5_0_OR_GREATER
using Microsoft.AspNetCore.Http;
#endif

namespace SaasBuilder.SharedKernel.Tenancy.Resolution;

#if NET5_0_OR_GREATER
/// <summary>
/// Resolves a tenant identifier from an incoming HTTP request.
/// Multiple resolvers are evaluated in descending <see cref="Priority"/> order;
/// the first non-<see langword="null"/> result wins.
/// </summary>
/// <remarks>
/// Built-in priority defaults (higher = tried first):
/// <list type="bullet">
///   <item>100 — <c>JwtClaimTenantResolver</c></item>
///   <item>50 — <c>HeaderTenantResolver</c></item>
///   <item>30 — <c>PathTenantResolver</c></item>
///   <item>20 — <c>SubdomainTenantResolver</c></item>
/// </list>
/// </remarks>
public interface ITenantResolver
{
    /// <summary>
    /// Gets the evaluation priority. Higher numbers are tried first.
    /// Resolvers with the same priority are evaluated in DI registration order.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Attempts to resolve the tenant identifier from the given HTTP context.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The resolved <see cref="Guid"/>, or <see langword="null"/> if this resolver cannot determine the tenant.</returns>
    ValueTask<Guid?> ResolveAsync(HttpContext context, CancellationToken ct = default);
}
#endif
