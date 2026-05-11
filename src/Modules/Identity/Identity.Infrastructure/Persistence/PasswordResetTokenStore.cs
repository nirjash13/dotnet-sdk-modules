using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Auth;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IPasswordResetTokenStore"/>.
/// </summary>
internal sealed class PasswordResetTokenStore(IdentityDbContext context) : IPasswordResetTokenStore
{
    /// <inheritdoc />
    public async Task<PasswordResetToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await context.PasswordResetTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.UsedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(PasswordResetToken token) => context.PasswordResetTokens.Add(token);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
