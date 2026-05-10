using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;

namespace Identity.Application.Organizations;

/// <summary>
/// Repository abstraction for <see cref="Organization"/> aggregate persistence.
/// Implemented in Infrastructure by EF Core. Application layer consumes this interface only.
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>Finds an organization by id within the current tenant, or returns <see langword="null"/>.</summary>
    Task<Organization?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds an organization by slug within the current tenant, or returns <see langword="null"/>.</summary>
    Task<Organization?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Adds a new organization to the store (no save; call SaveChangesAsync separately).</summary>
    void Add(Organization organization);

    /// <summary>Persists all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
