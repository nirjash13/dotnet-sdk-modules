using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Identity.Application.Services;
using Identity.Domain.Entities;
using SaasBuilder.SharedKernel.Abstractions;

namespace Identity.Application.Lifecycle;

/// <summary>
/// Handles <see cref="HardDeleteExpiredAccountsCommand"/>.
/// Finds users whose <c>DeletionScheduledFor</c> is in the past, redacts their PII,
/// and inserts <see cref="UserTombstone"/> records.
/// </summary>
public sealed class HardDeleteExpiredAccountsHandler
{
    private readonly IUserRepository _users;
    private readonly IUserTombstoneRepository _tombstones;

    /// <summary>Initializes a new instance of <see cref="HardDeleteExpiredAccountsHandler"/>.</summary>
    public HardDeleteExpiredAccountsHandler(
        IUserRepository users,
        IUserTombstoneRepository tombstones)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _tombstones = tombstones ?? throw new ArgumentNullException(nameof(tombstones));
    }

    /// <summary>Handles the command.</summary>
    public async Task<Result<int>> HandleAsync(
        HardDeleteExpiredAccountsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        IReadOnlyList<User> expired = await _users
            .FindExpiredDeletionsAsync(DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        int count = 0;
        foreach (User user in expired)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            DateTimeOffset deletedAt = user.DeletedAt!.Value;
            Guid userId = user.Id;

            user.RedactForHardDelete();

            UserTombstone tombstone = UserTombstone.Create(userId, deletedAt);
            _tombstones.Add(tombstone);

            count++;
        }

        await _users.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<int>.Success(count);
    }
}
