using System;

namespace Marketplace.Domain.Entities;

/// <summary>A published application entry in the marketplace directory.</summary>
public sealed class MarketplaceApp
{
    /// <summary>Gets the unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the URL-safe slug (e.g. "slack-connector").</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>Gets the display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets the vendor / publisher display name.</summary>
    public string Vendor { get; private set; } = string.Empty;

    /// <summary>Gets the short description shown in the marketplace listing.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the CDN URL of the app's icon (64×64 recommended).</summary>
    public string? IconUrl { get; private set; }

    /// <summary>Gets the raw app manifest JSON (OpenAPI extension, required scopes, webhook events).</summary>
    public string ManifestJson { get; private set; } = "{}";

    /// <summary>Gets the publisher account identifier in the SaaS tenant system.</summary>
    public Guid PublisherId { get; private set; }

    /// <summary>Gets a value indicating whether this app appears in the public listing.</summary>
    public bool IsListed { get; private set; }

    /// <summary>Gets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core parameterless constructor.
    private MarketplaceApp() { }

    /// <summary>Creates a new marketplace app entry.</summary>
    public static MarketplaceApp Create(
        string slug,
        string name,
        string vendor,
        string description,
        Guid publisherId,
        string? iconUrl = null,
        string? manifestJson = null)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug must not be empty.", nameof(slug));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must not be empty.", nameof(name));
        }

        return new MarketplaceApp
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = name,
            Vendor = vendor,
            Description = description,
            PublisherId = publisherId,
            IconUrl = iconUrl,
            ManifestJson = manifestJson ?? "{}",
            IsListed = false, // apps start unlisted; a publisher action publishes them
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Makes the app visible in the public marketplace listing.</summary>
    public void Publish() => IsListed = true;

    /// <summary>Removes the app from the public listing without deleting it.</summary>
    public void Unpublish() => IsListed = false;
}
