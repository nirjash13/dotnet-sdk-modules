using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>.
/// </summary>
internal sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    /// <inheritdoc />
    public async Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.Users
            .Include(u => u.Memberships)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await db.Users
            .Include(u => u.Memberships)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Email == email.Trim().ToLowerInvariant(),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<UserTenantMembership?> FindPrimaryMembershipAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.UserTenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.IsPrimary,
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public void Add(User user) => db.Users.Add(user);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
