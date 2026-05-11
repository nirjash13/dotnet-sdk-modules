using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="DeleteDomainClaimCommand"/>. Removes the domain claim from the store.
/// </summary>
public sealed class DeleteDomainClaimHandler
{
    private readonly IOrganizationDomainClaimRepository _claims;

    /// <summary>Initializes a new instance of <see cref="DeleteDomainClaimHandler"/>.</summary>
    public DeleteDomainClaimHandler(IOrganizationDomainClaimRepository claims)
    {
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
    }

    /// <summary>Handles the command.</summary>
    public async Task<Result> HandleAsync(
        DeleteDomainClaimCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        OrganizationDomainClaim? claim = await _claims
            .FindByIdAsync(command.ClaimId, cancellationToken)
            .ConfigureAwait(false);

        if (claim is null || claim.OrganizationId != command.OrganizationId)
        {
            return Result.Failure("Domain claim not found.");
        }

        _claims.Remove(claim);
        await _claims.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
