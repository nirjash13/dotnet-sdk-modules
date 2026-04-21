using System.Threading.Tasks;
using Identity.Application.Services;
using OpenIddict.Server;

namespace Identity.Infrastructure.Claims;

/// <summary>
/// Scoped OpenIddict server handler that bridges the <c>ProcessSignInContext</c> event
/// to the scoped <see cref="ITenantClaimEnricher"/> service.
/// </summary>
/// <remarks>
/// Registered as a scoped handler so that <see cref="ITenantClaimEnricher"/> and its
/// dependencies (<c>IUserRepository</c>) resolve from the request DI scope.
/// </remarks>
internal sealed class TenantClaimEventHandler(ITenantClaimEnricher enricher)
    : IOpenIddictServerHandler<OpenIddict.Server.OpenIddictServerEvents.ProcessSignInContext>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(OpenIddict.Server.OpenIddictServerEvents.ProcessSignInContext context)
    {
        // Forward as object — keeps Application layer interface decoupled from OpenIddict types.
        await enricher.EnrichAsync(context).ConfigureAwait(false);
    }
}
