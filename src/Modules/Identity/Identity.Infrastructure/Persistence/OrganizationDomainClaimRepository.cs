using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Organizations;
using Identity.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IOrganizationDomainClaimRepository"/>.
/// </summary>
internal sealed class OrganizationDomainClaimRepository(IdentityDbContext db)
    : IOrganizationDomainClaimRepository
{
    /// <inheritdoc />
    public async Task<OrganizationDomainClaim?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await db.OrganizationDomainClaims
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<OrganizationDomainClaim?> FindVerifiedByDomainAsync(
        string domain,
        CancellationToken cancellationToken = default)
        => await db.OrganizationDomainClaims
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Domain == domain.ToLowerInvariant() && c.VerifiedAt != null,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> DomainIsTakenAsync(
        string domain,
        CancellationToken cancellationToken = default)
        => await db.OrganizationDomainClaims
            .AnyAsync(c => c.Domain == domain.ToLowerInvariant(), cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public void Add(OrganizationDomainClaim claim) => db.OrganizationDomainClaims.Add(claim);

    /// <inheritdoc />
    public void Remove(OrganizationDomainClaim claim) => db.OrganizationDomainClaims.Remove(claim);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
