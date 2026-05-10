using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Organizations;
using Identity.Domain.Organizations;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IOrganizationRepository"/>.
/// All reads use <c>AsNoTracking()</c> except when the aggregate must be mutated.
/// </summary>
internal sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly IdentityDbContext _context;

    /// <summary>Initializes a new instance of <see cref="OrganizationRepository"/>.</summary>
    public OrganizationRepository(IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracking query: caller may need to mutate the aggregate.
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Organization?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        // AsNoTracking: slug lookup is a read-only check.
        return await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.Slug == slug.Trim().ToLowerInvariant(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(Organization organization) => _context.Organizations.Add(organization);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
