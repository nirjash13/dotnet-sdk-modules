using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Mfa;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ITotpCredentialStore"/>.
/// </summary>
internal sealed class TotpCredentialStore(IdentityDbContext context) : ITotpCredentialStore
{
    /// <inheritdoc />
    public async Task<TotpCredential?> FindByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await context.TotpCredentials
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(TotpCredential credential) => context.TotpCredentials.Add(credential);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
