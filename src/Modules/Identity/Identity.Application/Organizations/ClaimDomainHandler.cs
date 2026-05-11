using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="ClaimDomainCommand"/>.
/// Creates a pending <see cref="OrganizationDomainClaim"/> and returns the DNS TXT challenge token.
/// </summary>
public sealed class ClaimDomainHandler
{
    private readonly IOrganizationDomainClaimRepository _claims;

    /// <summary>Initializes a new instance of <see cref="ClaimDomainHandler"/>.</summary>
    public ClaimDomainHandler(IOrganizationDomainClaimRepository claims)
    {
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
    }

    /// <summary>Handles the command.</summary>
    public async Task<Result<ClaimDomainResult>> HandleAsync(
        ClaimDomainCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        string domain = command.Domain.Trim().ToLowerInvariant();

        bool isTaken = await _claims
            .DomainIsTakenAsync(domain, cancellationToken)
            .ConfigureAwait(false);

        if (isTaken)
        {
            return Result<ClaimDomainResult>.Failure(
                $"Domain '{domain}' is already claimed by another organization.");
        }

        OrganizationDomainClaim claim = OrganizationDomainClaim.Create(command.OrganizationId, domain);
        _claims.Add(claim);
        await _claims.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ClaimDomainResult>.Success(
            new ClaimDomainResult(claim.Id, claim.VerificationToken!));
    }
}
