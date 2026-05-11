using System;

namespace Gdpr.Contracts;

/// <summary>Represents the latest consent record for a user and consent key.</summary>
public sealed record ConsentDto(
    Guid Id,
    Guid UserId,
    Guid TenantId,
    string ConsentKey,
    bool Granted,
    string Version,
    DateTimeOffset Timestamp);
