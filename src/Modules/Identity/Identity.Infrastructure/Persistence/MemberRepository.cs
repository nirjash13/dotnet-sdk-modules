using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Organizations;
using Identity.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IMemberRepository"/>.
/// </summary>
internal sealed class MemberRepository : IMemberRepository
{
    private readonly IdentityDbContext _context;

    /// <summary>Initializes a new instance of <see cref="MemberRepository"/>.</summary>
    public MemberRepository(IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Member?> FindByIdAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        return await _context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Member>> GetActiveByOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Status == MemberStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(Member member) => _context.OrganizationMembers.Add(member);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
