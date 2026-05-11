using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marketplace.Application.Abstractions;
using Marketplace.Contracts;
using Marketplace.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Marketplace.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the Marketplace module.
/// Maps endpoints under <c>/api/v1/marketplace</c>.
/// Public listing endpoints are <c>[AllowAnonymous]</c>; all others require auth.
/// </summary>
public sealed class MarketplaceModule : IModuleStartup
{
    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddMarketplaceInfrastructure(config);
    }

#if NET10_0_OR_GREATER
    /// <inheritdoc/>
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        // Public endpoints — no auth required.
        RouteGroupBuilder publicGroup = endpoints
            .MapGroup("/api/v1/marketplace")
            .WithTags("marketplace")
            .AllowAnonymous();

        // GET /api/v1/marketplace/apps
        publicGroup.MapGet("/apps", ListAppsAsync)
            .WithName("Marketplace_ListApps")
            .WithSummary("Returns the public marketplace app catalogue.");

        // GET /api/v1/marketplace/apps/{slug}
        publicGroup.MapGet("/apps/{slug}", GetAppBySlugAsync)
            .WithName("Marketplace_GetApp")
            .WithSummary("Returns a single app by slug.");

        // Authenticated endpoints.
        RouteGroupBuilder authGroup = endpoints
            .MapGroup("/api/v1/marketplace")
            .WithTags("marketplace")
            .RequireAuthorization();

        // POST /api/v1/marketplace/apps/{slug}/install
        authGroup.MapPost("/apps/{slug}/install", InitiateInstallAsync)
            .WithName("Marketplace_InitiateInstall")
            .WithSummary("Initiates an OAuth-app install flow and returns the consent URL.");

        // POST /api/v1/marketplace/installations/{id}/approve
        authGroup.MapPost("/installations/{id:guid}/approve", ApproveInstallAsync)
            .WithName("Marketplace_ApproveInstall")
            .WithSummary("Admin approval for a pending installation.");

        // DELETE /api/v1/marketplace/installations/{id}
        authGroup.MapDelete("/installations/{id:guid}", UninstallAsync)
            .WithName("Marketplace_Uninstall")
            .WithSummary("Uninstalls an app from the current tenant.");

        // GET /api/v1/marketplace/installations/me
        authGroup.MapGet("/installations/me", GetMyInstallationsAsync)
            .WithName("Marketplace_MyInstallations")
            .WithSummary("Returns the current tenant's active app installations.");
    }

    private static async Task<IResult> ListAppsAsync(
        IMarketplaceAppRegistry registry,
        CancellationToken ct)
    {
        IReadOnlyList<MarketplaceAppDto> apps = await registry.ListAppsAsync(ct).ConfigureAwait(false);
        return Results.Ok(apps);
    }

    private static async Task<IResult> GetAppBySlugAsync(
        string slug,
        IMarketplaceAppRegistry registry,
        CancellationToken ct)
    {
        Result<MarketplaceAppDto> result = await registry.GetBySlugAsync(slug, ct).ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.NotFound(new { detail = result.Error });
    }

    private static async Task<IResult> InitiateInstallAsync(
        string slug,
        InstallRequest? request,
        IAppInstallationService installService,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        string returnUrl = request?.ReturnUrl ?? "/marketplace/installed";

        // UserId from claims — use Empty as placeholder; resolved from HttpContext in full impl.
        Result<InitiateInstallResponse> result = await installService
            .InitiateInstallAsync(slug, tenant.TenantId, Guid.Empty, returnUrl, ct)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ApproveInstallAsync(
        Guid id,
        IAppInstallationService installService,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        if (tenantAccessor.Current is null)
        {
            return Results.Unauthorized();
        }

        Result<AppInstallationDto> result = await installService
            .ApproveInstallAsync(id, ct)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> UninstallAsync(
        Guid id,
        IAppInstallationService installService,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        Result<bool> result = await installService
            .UninstallAsync(id, tenant.TenantId, ct)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Results.NoContent()
            : Results.Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetMyInstallationsAsync(
        IAppInstallationService installService,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        IReadOnlyList<AppInstallationDto> installations = await installService
            .GetTenantInstallationsAsync(tenant.TenantId, ct)
            .ConfigureAwait(false);

        return Results.Ok(installations);
    }
#endif

    /// <summary>Request body for initiating an install.</summary>
    public sealed class InstallRequest
    {
        /// <summary>Gets or sets the return URL after OAuth consent.</summary>
        public string? ReturnUrl { get; set; }
    }
}
