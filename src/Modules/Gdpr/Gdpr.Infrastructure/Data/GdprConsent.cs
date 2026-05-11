using System;

namespace Gdpr.Infrastructure.Data;

/// <summary>Append-only consent record stored in <c>gdpr_consents</c>.</summary>
internal sealed class GdprConsent
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid UserId { get; init; }

    public string ConsentKey { get; init; } = string.Empty;

    public bool Granted { get; init; }

    public string Version { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }
}
