using Identity.Domain.Entities;

namespace Identity.Application.Lifecycle;

/// <summary>Repository for <see cref="UserTombstone"/> persistence.</summary>
public interface IUserTombstoneRepository
{
    /// <summary>Adds a tombstone (no save).</summary>
    void Add(UserTombstone tombstone);
}
