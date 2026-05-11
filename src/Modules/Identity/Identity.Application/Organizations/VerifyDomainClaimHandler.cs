using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Organizations;

/// <summary>
/// Handles <see cref="VerifyDomainClaimCommand"/>.
/// Calls <see cref="IDomainOwnershipVerifier"/> to check the DNS TXT record
/// and marks the claim as verified if successful.
/// </summary>
public sealed class VerifyDomainClaimHandler
{
    private readonly IOrganizationDomainClaimRepository _claims;
    private readonly IDomainOwnershipVerifier _verifier;

    /// <summary>Initializes a new instance of <see cref="VerifyDomainClaimHandler"/>.</summary>
    public VerifyDomainClaimHandler(
        IOrganizationDomainClaimRepository claims,
        IDomainOwnershipVerifier verifier)
    {
        _claims = claims ?? throw new ArgumentNullException(nameof(claims));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
    }

    /// <summary>Handles the command.</summary>
    public async Task<Result> HandleAsync(
        VerifyDomainClaimCommand command,
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

        if (claim.IsVerified)
        {
            return Result.Success();
        }

        if (string.IsNullOrEmpty(claim.VerificationToken))
        {
            return Result.Failure("Claim has no verification token.");
        }

        bool verified = await _verifier
            .VerifyAsync(claim.Domain, claim.VerificationToken, cancellationToken)
            .ConfigureAwait(false);

        if (!verified)
        {
            return Result.Failure(
                $"DNS TXT record for '_saasbuilder-verify.{claim.Domain}' not found or does not match the token. " +
                "DNS propagation can take up to 48 hours.");
        }

        claim.MarkVerified();
        await _claims.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
