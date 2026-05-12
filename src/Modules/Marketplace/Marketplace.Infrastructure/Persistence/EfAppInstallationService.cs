using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Marketplace.Application.Abstractions;
using Marketplace.Contracts;
using Marketplace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Marketplace.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IAppInstallationService"/>.</summary>
internal sealed class EfAppInstallationService : IAppInstallationService
{
    private readonly MarketplaceDbContext _db;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILogger<EfAppInstallationService> _logger;

    public EfAppInstallationService(
        MarketplaceDbContext db,
        ITenantContextAccessor tenantContextAccessor,
        ILogger<EfAppInstallationService> logger)
    {
        _db = db;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<InitiateInstallResponse>> InitiateInstallAsync(
        string slug,
        Guid tenantId,
        Guid requestingUserId,
        string returnUrl,
        CancellationToken ct = default)
    {
        MarketplaceApp? app = await _db.Apps
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == slug, ct)
            .ConfigureAwait(false);

        if (app is null)
        {
            return Result<InitiateInstallResponse>.Failure($"App '{slug}' not found.");
        }

        // Generate a synthetic OAuth client ID for this installation.
        string oauthClientId = $"app_{app.Id:N}_{tenantId:N}";

        // Start with empty scopes — user consents during OAuth flow.
        AppInstallation installation = AppInstallation.CreatePending(
            tenantId,
            app.Id,
            oauthClientId,
            Array.Empty<string>(),
            requestingUserId);

        _db.Installations.Add(installation);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // GAP: Real OAuth consent URL would be built from OpenIddict's authorization endpoint.
        string consentUrl =
            $"/connect/authorize?client_id={oauthClientId}&redirect_uri={Uri.EscapeDataString(returnUrl)}" +
            $"&response_type=code&scope=openid&state={installation.Id:N}";

        _logger.LogInformation(
            "Marketplace install initiated: appSlug={Slug}, tenantId={TenantId}, installId={InstallId}",
            slug, tenantId, installation.Id);

        return Result<InitiateInstallResponse>.Success(new InitiateInstallResponse
        {
            InstallationId = installation.Id,
            ConsentUrl = consentUrl,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<AppInstallationDto>> CompleteInstallAsync(
        Guid installationId,
        IReadOnlyList<string> grantedScopes,
        CancellationToken ct = default)
    {
        AppInstallation? installation = await _db.Installations
            .FirstOrDefaultAsync(i => i.Id == installationId, ct)
            .ConfigureAwait(false);

        if (installation is null)
        {
            return Result<AppInstallationDto>.Failure($"Installation '{installationId}' not found.");
        }

        if (installation.Status != AppInstallationStatus.Pending)
        {
            return Result<AppInstallationDto>.Failure(
                $"Installation is in status '{installation.Status}', not Pending.");
        }

        // Update the granted scopes and approve.
        // Granted scopes must be stored on the entity via direct SQL update since the
        // domain entity uses private setters. Use a tracked update approach.
        _db.Entry(installation).Property("GrantedScopesJson").CurrentValue =
            JsonSerializer.Serialize(grantedScopes);

        installation.Approve();
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<AppInstallationDto>.Success(await ToDto(installation, ct).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<Result<AppInstallationDto>> ApproveInstallAsync(
        Guid installationId,
        CancellationToken ct = default)
    {
        // C-5: include tenant predicate so an admin from tenant A cannot approve
        // an installation belonging to tenant B by guessing the Guid.
        Guid? callerTenantId = _tenantContextAccessor.Current?.TenantId;
        if (callerTenantId is null)
        {
            return Result<AppInstallationDto>.Failure("No tenant context for approve operation.");
        }

        AppInstallation? installation = await _db.Installations
            .FirstOrDefaultAsync(i => i.Id == installationId && i.TenantId == callerTenantId.Value, ct)
            .ConfigureAwait(false);

        if (installation is null)
        {
            return Result<AppInstallationDto>.Failure($"Installation '{installationId}' not found.");
        }

        try
        {
            installation.Approve();
        }
        catch (InvalidOperationException ex)
        {
            return Result<AppInstallationDto>.Failure(ex.Message);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<AppInstallationDto>.Success(await ToDto(installation, ct).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> UninstallAsync(
        Guid installationId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        AppInstallation? installation = await _db.Installations
            .FirstOrDefaultAsync(i => i.Id == installationId && i.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        if (installation is null)
        {
            // S1 (C-6): check whether the record exists but belongs to a different tenant.
            // If so, emit an audit log entry to surface potential cross-tenant probing.
            bool existsElsewhere = await _db.Installations
                .AsNoTracking()
                .AnyAsync(i => i.Id == installationId, ct)
                .ConfigureAwait(false);

            if (existsElsewhere)
            {
                _logger.LogWarning(
                    "Cross-tenant uninstall attempt: installId={InstallId} exists but does not belong to tenantId={TenantId}. Request denied.",
                    installationId,
                    tenantId);
            }

            return Result<bool>.Failure("not_found");
        }

        try
        {
            installation.Uninstall();
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Marketplace app uninstalled: installId={InstallId}, tenantId={TenantId}",
            installationId, tenantId);

        return Result<bool>.Success(true);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AppInstallationDto>> GetTenantInstallationsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var installations = await _db.Installations
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.Status != AppInstallationStatus.Uninstalled)
            .Join(
                _db.Apps,
                i => i.AppId,
                a => a.Id,
                (i, a) => new { Installation = i, App = a })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return installations.Select(row => MapToDto(row.Installation, row.App)).ToList();
    }

    private async Task<AppInstallationDto> ToDto(AppInstallation installation, CancellationToken ct)
    {
        MarketplaceApp? app = await _db.Apps
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == installation.AppId, ct)
            .ConfigureAwait(false);

        return MapToDto(installation, app);
    }

    private static AppInstallationDto MapToDto(AppInstallation installation, MarketplaceApp? app)
    {
        IReadOnlyList<string> scopes;
        try
        {
            scopes = (IReadOnlyList<string>?)JsonSerializer.Deserialize<List<string>>(installation.GrantedScopesJson)
                ?? Array.Empty<string>();
        }
        catch
        {
            scopes = Array.Empty<string>();
        }

        return new AppInstallationDto
        {
            Id = installation.Id,
            TenantId = installation.TenantId,
            AppId = installation.AppId,
            AppSlug = app?.Slug ?? string.Empty,
            AppName = app?.Name ?? string.Empty,
            GrantedScopes = scopes,
            Status = installation.Status.ToString(),
            InstalledAt = installation.InstalledAt,
        };
    }
}
