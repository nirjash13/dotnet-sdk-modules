using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Entities;

namespace Identity.Application.Impersonation;

/// <summary>
/// Persistence abstraction for <see cref="ImpersonationSessionEntity"/> records.
/// </summary>
public interface IImpersonationSessionStore
{
    /// <summary>Finds an active session by its unique identifier.</summary>
    Task<ImpersonationSessionEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Persists a new session.</summary>
    void Add(ImpersonationSessionEntity session);

    /// <summary>Saves all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
