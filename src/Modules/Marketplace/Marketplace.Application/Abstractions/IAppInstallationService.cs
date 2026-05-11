using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marketplace.Contracts;
using SaasBuilder.SharedKernel.Abstractions;

namespace Marketplace.Application.Abstractions;

/// <summary>
/// Orchestrates the full lifecycle of a marketplace app installation:
/// OAuth initiation → consent → activation → uninstall.
/// </summary>
public interface IAppInstallationService
{
    /// <summary>
    /// Starts the installation flow for the given app slug.
    /// Creates a <c>Pending</c> installation record and returns the OAuth consent URL.
    /// </summary>
    /// <param name="slug">The app slug to install.</param>
    /// <param name="tenantId">The installing tenant.</param>
    /// <param name="requestingUserId">The user performing the install.</param>
    /// <param name="returnUrl">The URL to redirect to after OAuth consent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pending installation ID and OAuth consent URL.</returns>
    Task<Result<InitiateInstallResponse>> InitiateInstallAsync(
        string slug,
        Guid tenantId,
        Guid requestingUserId,
        string returnUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Completes the installation after the user has granted consent.
    /// Transitions the installation from <c>Pending</c> to <c>Active</c>
    /// and stores the granted scopes.
    /// </summary>
    Task<Result<AppInstallationDto>> CompleteInstallAsync(
        Guid installationId,
        IReadOnlyList<string> grantedScopes,
        CancellationToken ct = default);

    /// <summary>
    /// Admin approval step (required for high-sensitivity apps).
    /// Transitions the installation from <c>Pending</c> to <c>Active</c>.
    /// </summary>
    Task<Result<AppInstallationDto>> ApproveInstallAsync(
        Guid installationId,
        CancellationToken ct = default);

    /// <summary>Uninstalls an app. Transitions status to <c>Uninstalled</c>.</summary>
    Task<Result<bool>> UninstallAsync(Guid installationId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Returns all installations for the given tenant.</summary>
    Task<IReadOnlyList<AppInstallationDto>> GetTenantInstallationsAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
