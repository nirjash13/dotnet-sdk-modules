using System;
using System.Collections.Generic;

namespace SaasBuilder.Host.Tenancy;

/// <summary>
/// Snapshot of tenancy options consumed by <see cref="TenantMiddleware"/>.
/// Registered as a singleton via <see cref="Microsoft.Extensions.Options.IOptions{T}"/>
/// so the middleware constructor resolves it without capturing mutable DI state.
/// </summary>
internal sealed class TenantMiddlewareOptions
{
    /// <summary>
    /// Gets the set of request path prefixes that bypass tenant enforcement.
    /// </summary>
    public IReadOnlySet<string> AnonymousBypass { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
