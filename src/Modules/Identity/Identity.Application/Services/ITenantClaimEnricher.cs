using System.Threading;
using System.Threading.Tasks;

namespace Identity.Application.Services;

/// <summary>
/// Application-layer interface for enriching issued tokens with tenant-specific claims.
/// Implemented in Infrastructure by <c>TenantClaimEnricher</c> which reads the current
/// tenant context and the user's primary <c>UserTenantMembership</c>.
/// </summary>
public interface ITenantClaimEnricher
{
    /// <summary>
    /// Injects tenant claims (<c>tenant_id</c>, <c>roles</c>, <c>membership_id</c>)
    /// into the OpenIddict sign-in context principal.
    /// </summary>
    /// <param name="context">The OpenIddict process-sign-in context. Typed as object to avoid
    /// a dependency on OpenIddict in the Application layer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnrichAsync(object context, CancellationToken cancellationToken = default);
}
