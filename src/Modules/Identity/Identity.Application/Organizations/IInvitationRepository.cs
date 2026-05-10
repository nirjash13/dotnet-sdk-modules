using System;
using System.Threading;
using System.Threading.Tasks;
using Identity.Domain.Organizations;

namespace Identity.Application.Organizations;

/// <summary>
/// Repository abstraction for <see cref="Invitation"/> persistence.
/// </summary>
public interface IInvitationRepository
{
    /// <summary>Finds an invitation by its id, or returns <see langword="null"/>.</summary>
    Task<Invitation?> FindByIdAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a pending invitation by its hashed token, or returns <see langword="null"/>.
    /// Only returns invitations that have not been redeemed or revoked.
    /// </summary>
    Task<Invitation?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Adds a new invitation (no save; call SaveChangesAsync separately).</summary>
    void Add(Invitation invitation);

    /// <summary>Persists all pending changes to the database.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
