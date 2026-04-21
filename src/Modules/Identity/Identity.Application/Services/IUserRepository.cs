using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Entities;

namespace Identity.Application.Services;

/// <summary>
/// Repository abstraction for <see cref="User"/> aggregate persistence.
/// Implemented in Infrastructure by EF Core. Application layer consumes this interface only.
/// </summary>
public interface IUserRepository
{
    /// <summary>Finds a user by their unique identifier, or returns <see langword="null"/>.</summary>
    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Finds a user by their email address, or returns <see langword="null"/>.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the primary <see cref="UserTenantMembership"/> for the given user.
    /// Returns <see langword="null"/> if the user has no memberships.
    /// </summary>
    Task<UserTenantMembership?> FindPrimaryMembershipAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new user to the persistent store (no save; call SaveChangesAsync separately).</summary>
    void Add(User user);

    /// <summary>Persists all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
