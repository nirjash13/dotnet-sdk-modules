using System;
using System.Collections.Generic;
using SaasBuilder.SharedKernel.Tenancy;

namespace Marketplace.Domain.Entities;

/// <summary>
/// Records a tenant's installation of a marketplace app including the OAuth grant and scopes.
/// Tenant-scoped: each tenant has its own installation record per app.
/// </summary>
public sealed class AppInstallation : ITenantScoped
{
    /// <summary>Gets the unique installation identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the marketplace app that was installed.</summary>
    public Guid AppId { get; private set; }

    /// <summary>Gets the OAuth client identifier issued to the installed app.</summary>
    public string OAuthClientId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the scopes granted by the tenant during the OAuth consent flow.
    /// Stored as a JSON array (e.g. '["read:data","write:webhooks"]').
    /// </summary>
    public string GrantedScopesJson { get; private set; } = "[]";

    /// <summary>Gets the user who performed the installation.</summary>
    public Guid InstalledByUserId { get; private set; }

    /// <summary>Gets the UTC timestamp when the installation was recorded.</summary>
    public DateTimeOffset InstalledAt { get; private set; }

    /// <summary>Gets the current lifecycle status of this installation.</summary>
    public AppInstallationStatus Status { get; private set; }

    // EF Core parameterless constructor.
    private AppInstallation() { }

    /// <summary>Creates a new installation record in <see cref="AppInstallationStatus.Pending"/> state.</summary>
    public static AppInstallation CreatePending(
        Guid tenantId,
        Guid appId,
        string oAuthClientId,
        IReadOnlyList<string> grantedScopes,
        Guid installedByUserId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(oAuthClientId))
        {
            throw new ArgumentException("OAuthClientId must not be empty.", nameof(oAuthClientId));
        }

        string scopesJson = System.Text.Json.JsonSerializer.Serialize(grantedScopes);

        return new AppInstallation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AppId = appId,
            OAuthClientId = oAuthClientId,
            GrantedScopesJson = scopesJson,
            InstalledByUserId = installedByUserId,
            InstalledAt = DateTimeOffset.UtcNow,
            Status = AppInstallationStatus.Pending,
        };
    }

    /// <summary>Transitions the installation from Pending to Active.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the current status is not Pending.</exception>
    public void Approve()
    {
        if (Status != AppInstallationStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot approve installation in status '{Status}'. Only Pending installations can be approved.");
        }

        Status = AppInstallationStatus.Active;
    }

    /// <summary>Suspends an Active installation.</summary>
    public void Suspend()
    {
        if (Status != AppInstallationStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot suspend installation in status '{Status}'. Only Active installations can be suspended.");
        }

        Status = AppInstallationStatus.Suspended;
    }

    /// <summary>Marks the installation as uninstalled.</summary>
    public void Uninstall()
    {
        if (Status == AppInstallationStatus.Uninstalled)
        {
            throw new InvalidOperationException("Installation is already uninstalled.");
        }

        Status = AppInstallationStatus.Uninstalled;
    }
}
