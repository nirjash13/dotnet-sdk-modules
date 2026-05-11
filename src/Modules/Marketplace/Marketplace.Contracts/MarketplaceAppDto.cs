using System;

namespace Marketplace.Contracts;

/// <summary>Read-model for a marketplace app entry.</summary>
public sealed class MarketplaceAppDto
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the URL-safe slug.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the vendor name.</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>Gets or sets the short description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the icon URL.</summary>
    public string? IconUrl { get; set; }
}
