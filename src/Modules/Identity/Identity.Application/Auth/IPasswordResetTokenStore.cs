using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Entities;

namespace Identity.Application.Auth;

/// <summary>
/// Persistence abstraction for <see cref="PasswordResetToken"/> entities.
/// </summary>
public interface IPasswordResetTokenStore
{
    /// <summary>Finds an active token by its SHA-256 hash, or returns <see langword="null"/>.</summary>
    Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Persists a new token.</summary>
    void Add(PasswordResetToken token);

    /// <summary>Saves all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
