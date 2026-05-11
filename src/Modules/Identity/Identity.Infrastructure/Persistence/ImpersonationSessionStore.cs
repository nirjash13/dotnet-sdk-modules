using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Impersonation;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IImpersonationSessionStore"/>.
/// </summary>
internal sealed class ImpersonationSessionStore(IdentityDbContext context) : IImpersonationSessionStore
{
    /// <inheritdoc />
    public async Task<ImpersonationSessionEntity?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await context.ImpersonationSessions
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(ImpersonationSessionEntity session) => context.ImpersonationSessions.Add(session);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
