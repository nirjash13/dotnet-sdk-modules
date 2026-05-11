using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Entities;

namespace Identity.Application.ApiKeys;

/// <summary>
/// Persistence abstraction for <see cref="ApiKey"/> entities.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>Finds an active API key by its SHA-256 hash.</summary>
    Task<ApiKey?> FindByHashAsync(string keyHash, CancellationToken cancellationToken = default);

    /// <summary>Finds an API key by its unique identifier.</summary>
    Task<ApiKey?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists all active API keys owned by the given user.</summary>
    Task<IReadOnlyList<ApiKey>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Persists a new API key.</summary>
    void Add(ApiKey apiKey);

    /// <summary>Saves all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
