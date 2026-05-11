using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Auth;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IEmailVerificationTokenStore"/>.
/// </summary>
internal sealed class EmailVerificationTokenStore(IdentityDbContext context) : IEmailVerificationTokenStore
{
    /// <inheritdoc />
    public async Task<EmailVerificationToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await context.EmailVerificationTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.UsedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(EmailVerificationToken token) => context.EmailVerificationTokens.Add(token);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
