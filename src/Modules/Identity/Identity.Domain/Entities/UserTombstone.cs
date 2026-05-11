using System;

namespace Identity.Domain.Entities;

/// <summary>
/// Preserves audit-history references after a user is hard-deleted.
/// The tombstone is a permanent record; it is never deleted.
/// </summary>
public sealed class UserTombstone
{
    private UserTombstone()
    {
    }

    /// <summary>Gets the tombstone identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the original user identifier (the value that appears in audit records).</summary>
    public Guid OriginalUserId { get; private set; }

    /// <summary>Gets the UTC time the soft-deletion was requested.</summary>
    public DateTimeOffset DeletedAt { get; private set; }

    /// <summary>Gets the UTC time the hard-deletion (PII redaction) was executed.</summary>
    public DateTimeOffset HardDeletedAt { get; private set; }

    /// <summary>Creates a tombstone for a user that has just been hard-deleted.</summary>
    public static UserTombstone Create(Guid originalUserId, DateTimeOffset deletedAt)
    {
        return new UserTombstone
        {
            Id = Guid.NewGuid(),
            OriginalUserId = originalUserId,
            DeletedAt = deletedAt,
            HardDeletedAt = DateTimeOffset.UtcNow,
        };
    }
}
