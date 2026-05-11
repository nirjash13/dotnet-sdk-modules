using System;

namespace Marketplace.Domain.Entities;

/// <summary>A permission scope that a marketplace app may request from tenants.</summary>
public sealed class AppScope
{
    /// <summary>Gets the marketplace app this scope belongs to.</summary>
    public Guid AppId { get; private set; }

    /// <summary>Gets the scope key (e.g. "read:contacts", "write:webhooks").</summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>Gets the human-readable description shown during consent.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the sensitivity level of this scope.
    /// Higher values (e.g. 3 = destructive) require additional approval steps.
    /// </summary>
    public int Sensitivity { get; private set; }

    // EF Core parameterless constructor.
    private AppScope() { }

    /// <summary>Creates a new scope definition for an app.</summary>
    public static AppScope Create(Guid appId, string scope, string description, int sensitivity = 1)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Scope must not be empty.", nameof(scope));
        }

        return new AppScope
        {
            AppId = appId,
            Scope = scope,
            Description = description,
            Sensitivity = Math.Clamp(sensitivity, 1, 5),
        };
    }
}
