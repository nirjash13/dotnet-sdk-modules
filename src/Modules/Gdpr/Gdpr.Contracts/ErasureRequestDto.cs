using System;

namespace Gdpr.Contracts;

/// <summary>Status of an erasure request.</summary>
public enum ErasureStatus
{
    /// <summary>Pending within the grace period.</summary>
    Pending,

    /// <summary>Cancelled by an administrator.</summary>
    Cancelled,

    /// <summary>Anonymization complete.</summary>
    Completed,
}

/// <summary>Represents an erasure (right-to-be-forgotten) request.</summary>
public sealed record ErasureRequestDto(
    Guid Id,
    Guid UserId,
    Guid TenantId,
    ErasureStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset GraceEndsAt,
    DateTimeOffset? CompletedAt);
