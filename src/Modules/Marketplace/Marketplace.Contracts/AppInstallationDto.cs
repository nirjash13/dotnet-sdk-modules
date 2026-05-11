using System;
using System.Collections.Generic;

namespace Marketplace.Contracts;

/// <summary>Read-model for a tenant's installed app.</summary>
public sealed class AppInstallationDto
{
    /// <summary>Gets or sets the installation identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Gets or sets the app identifier.</summary>
    public Guid AppId { get; set; }

    /// <summary>Gets or sets the app slug for display.</summary>
    public string AppSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the app name for display.</summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>Gets or sets the granted scopes.</summary>
    public IReadOnlyList<string> GrantedScopes { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the installation status string.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC installation timestamp.</summary>
    public DateTimeOffset InstalledAt { get; set; }
}
