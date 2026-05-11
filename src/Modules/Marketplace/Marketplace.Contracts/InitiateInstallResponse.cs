using System;

namespace Marketplace.Contracts;

/// <summary>Response to a marketplace app install initiation.</summary>
public sealed class InitiateInstallResponse
{
    /// <summary>Gets or sets the pending installation identifier.</summary>
    public Guid InstallationId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth consent URL the end-user should be redirected to
    /// to grant the requested scopes.
    /// </summary>
    public string ConsentUrl { get; set; } = string.Empty;
}
