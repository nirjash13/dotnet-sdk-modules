using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;

namespace Identity.Application.Organizations;

/// <summary>
/// Repository abstraction for <see cref="Member"/> aggregate persistence.
/// </summary>
public interface IMemberRepository
{
    /// <summary>Finds a member by id within an organization, or returns <see langword="null"/>.</summary>
    Task<Member?> FindByIdAsync(Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active members of the given organization.
    /// Used by owner-count protection checks.
    /// </summary>
    Task<IReadOnlyList<Member>> GetActiveByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new member record (no save; call SaveChangesAsync separately).</summary>
    void Add(Member member);

    /// <summary>Persists all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
