namespace Marketplace.Domain.Entities;

/// <summary>Lifecycle states of an app installation.</summary>
public enum AppInstallationStatus
{
    /// <summary>OAuth consent flow has been initiated but not yet completed.</summary>
    Pending,

    /// <summary>App is installed and fully operational.</summary>
    Active,

    /// <summary>App is temporarily suspended (e.g. billing failure or policy violation).</summary>
    Suspended,

    /// <summary>App has been uninstalled from the tenant.</summary>
    Uninstalled,
}
