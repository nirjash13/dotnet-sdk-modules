using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Organizations;
using Identity.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IInvitationRepository"/>.
/// </summary>
internal sealed class InvitationRepository : IInvitationRepository
{
    private readonly IdentityDbContext _context;

    /// <summary>Initializes a new instance of <see cref="InvitationRepository"/>.</summary>
    public InvitationRepository(IdentityDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Invitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return await _context.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.Invitations
            .FirstOrDefaultAsync(
                i => i.TokenHash == tokenHash && i.RedeemedAt == null && i.RevokedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Add(Invitation invitation) => _context.Invitations.Add(invitation);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
