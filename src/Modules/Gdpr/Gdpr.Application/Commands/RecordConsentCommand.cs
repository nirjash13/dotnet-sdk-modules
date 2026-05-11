using System;

namespace Gdpr.Application.Commands;

/// <summary>Records a consent decision for a user.</summary>
public sealed record RecordConsentCommand(
    Guid TenantId,
    Guid UserId,
    string ConsentKey,
    bool Granted,
    string Version);
