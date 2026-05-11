using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.ApiKeys;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IApiKeyStore"/>.
/// </summary>
internal sealed class ApiKeyStore(IdentityDbContext context) : IApiKeyStore
{
    /// <inheritdoc />
    public async Task<ApiKey?> FindByHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        return await context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.RevokedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ApiKey?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApiKey>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId && k.RevokedAt == null)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(ApiKey apiKey) => context.ApiKeys.Add(apiKey);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
