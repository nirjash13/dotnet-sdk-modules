using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Entities;

namespace Identity.Application.Mfa;

/// <summary>
/// Persistence abstraction for <see cref="TotpCredential"/> entities.
/// </summary>
public interface ITotpCredentialStore
{
    /// <summary>Finds the TOTP credential for the given user, or returns <see langword="null"/>.</summary>
    Task<TotpCredential?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new credential.</summary>
    void Add(TotpCredential credential);

    /// <summary>Saves all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
