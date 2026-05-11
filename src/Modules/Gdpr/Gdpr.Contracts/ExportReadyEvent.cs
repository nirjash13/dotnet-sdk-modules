using System;

namespace Gdpr.Contracts;

/// <summary>Published when a GDPR data export zip has been written to blob storage.</summary>
public sealed record ExportReadyEvent(
    Guid TenantId,
    Guid UserId,
    string DownloadUrl,
    DateTimeOffset ExpiresAt);
