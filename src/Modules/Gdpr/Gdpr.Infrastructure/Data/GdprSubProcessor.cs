using System;

namespace Gdpr.Infrastructure.Data;

/// <summary>A GDPR sub-processor entry stored in <c>gdpr_subprocessors</c>.</summary>
internal sealed class GdprSubProcessor
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string DataTypes { get; set; } = string.Empty;

    public string Website { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; set; }
}
