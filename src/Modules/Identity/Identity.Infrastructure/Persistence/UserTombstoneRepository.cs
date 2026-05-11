using Identity.Application.Lifecycle;
using Identity.Domain.Entities;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUserTombstoneRepository"/>.
/// </summary>
internal sealed class UserTombstoneRepository(IdentityDbContext db) : IUserTombstoneRepository
{
    /// <inheritdoc />
    public void Add(UserTombstone tombstone) => db.UserTombstones.Add(tombstone);
}
