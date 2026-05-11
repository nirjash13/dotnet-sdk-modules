using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;

namespace Identity.Application.Organizations;

/// <summary>
/// Repository abstraction for <see cref="OrganizationDomainClaim"/> persistence.
/// </summary>
public interface IOrganizationDomainClaimRepository
{
    /// <summary>Finds a domain claim by its identifier.</summary>
    Task<OrganizationDomainClaim?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a verified domain claim by the exact (lowercased) domain string.
    /// Returns null if no verified claim exists.
    /// </summary>
    Task<OrganizationDomainClaim?> FindVerifiedByDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if any claim (pending or verified) already exists for this domain.
    /// Used to prevent duplicate claims across organizations.
    /// </summary>
    Task<bool> DomainIsTakenAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>Adds a new claim to the store (no save).</summary>
    void Add(OrganizationDomainClaim claim);

    /// <summary>Removes a claim from the store (no save).</summary>
    void Remove(OrganizationDomainClaim claim);

    /// <summary>Persists pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
