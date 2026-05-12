using System;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Gdpr.Application.Abstractions;
using Gdpr.Application.Commands;
using Gdpr.Contracts;
using Gdpr.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SaasBuilder.SharedKernel.Abstractions;

namespace Gdpr.Api;

/// <summary><see cref="IModuleStartup"/> for the GDPR module.</summary>
public sealed class GdprModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.Configure<GdprOptions>(config.GetSection("Gdpr:Erasure"));
        services.AddGdprInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        // User-scoped GDPR endpoints (any authenticated user, own data only)
        RouteGroupBuilder userGroup = endpoints
            .MapGroup("/api/v1/gdpr")
            .WithTags("gdpr")
            .RequireAuthorization();

        // Export — user's own data
        userGroup.MapPost("/export/me", ExportMeAsync)
            .WithName("Gdpr_ExportMe")
            .WithSummary("Initiates a personal data export for the authenticated user.");

        // Erasure request
        userGroup.MapPost("/erasure/request", RequestErasureAsync)
            .WithName("Gdpr_RequestErasure")
            .WithSummary("Submits a right-to-be-forgotten request for the authenticated user.");

        // Consent
        userGroup.MapPost("/consent", RecordConsentAsync)
            .WithName("Gdpr_RecordConsent")
            .WithSummary("Records a consent decision for the authenticated user.");

        userGroup.MapGet("/consent/me/{key}", GetLatestConsentAsync)
            .WithName("Gdpr_GetConsent")
            .WithSummary("Returns the latest consent record for the authenticated user and key.");

        // DPA template
        userGroup.MapGet("/dpa-template", GetDpaTemplateAsync)
            .AllowAnonymous()
            .WithName("Gdpr_DpaTemplate")
            .WithSummary("Returns the DPA Markdown template.");

        // Sub-processors (public read, admin write)
        userGroup.MapGet("/sub-processors", GetSubProcessorsAsync)
            .AllowAnonymous()
            .WithName("Gdpr_GetSubProcessors")
            .WithSummary("Returns the list of GDPR sub-processors.");

        // Admin-scoped GDPR endpoints
        RouteGroupBuilder adminGroup = endpoints
            .MapGroup("/api/v1/gdpr")
            .WithTags("gdpr")
            .RequireAuthorization("AdminPolicy");

        adminGroup.MapPost("/export/{tenantId:guid}", ExportTenantAsync)
            .WithName("Gdpr_ExportTenant")
            .WithSummary("Initiates a data export for all users in a tenant. Admin-only.");

        adminGroup.MapDelete("/erasure/{id:guid}", CancelErasureAsync)
            .WithName("Gdpr_CancelErasure")
            .WithSummary("Cancels a pending erasure request. Admin-only.");

        adminGroup.MapPost("/sub-processors", CreateSubProcessorAsync)
            .WithName("Gdpr_CreateSubProcessor")
            .WithSummary("Adds a sub-processor. Admin-only.");

        adminGroup.MapDelete("/sub-processors/{id:guid}", DeleteSubProcessorAsync)
            .WithName("Gdpr_DeleteSubProcessor")
            .WithSummary("Removes a sub-processor. Admin-only.");
    }

    // ── Handler methods ───────────────────────────────────────────────────────
    private static async Task<IResult> ExportMeAsync(
        ClaimsPrincipal user,
        IDataExportBuilder exportBuilder,
        CancellationToken ct)
    {
        if (!TryGetUserAndTenant(user, out Guid userId, out Guid tenantId))
        {
            return Results.Unauthorized();
        }

        Stream zip = await exportBuilder.BuildAsync(tenantId, userId, ct).ConfigureAwait(false);
        return Results.File(zip, "application/zip", $"gdpr-export-{userId:N}.zip");
    }

    private static async Task<IResult> ExportTenantAsync(
        Guid tenantId,
        ClaimsPrincipal user,
        IDataExportBuilder exportBuilder,
        CancellationToken ct)
    {
        // C-7: validate the tenantId route parameter against the caller's tenant claim.
        // Without this check, an admin of tenant A could export tenant B's PII by
        // substituting tenant B's Guid in the route. Platform-wide admins (no tenant_id claim)
        // are permitted to export any tenant.
        string? callerTenantIdRaw = user.FindFirstValue("tenant_id");
        if (callerTenantIdRaw is not null)
        {
            if (!Guid.TryParse(callerTenantIdRaw, out Guid callerTenantId)
                || callerTenantId != tenantId)
            {
                return Results.Forbid();
            }
        }

        // Admin-scoped: exports all data for the tenant (userId = empty → all users).
        // IExportable implementations treat Guid.Empty as "all users in tenant".
        Stream zip = await exportBuilder.BuildAsync(tenantId, Guid.Empty, ct).ConfigureAwait(false);
        return Results.File(zip, "application/zip", $"gdpr-export-tenant-{tenantId:N}.zip");
    }

    private static async Task<IResult> RequestErasureAsync(
        ClaimsPrincipal user,
        IGdprErasureRepository erasureRepo,
        IOptions<GdprOptions> options,
        CancellationToken ct)
    {
        if (!TryGetUserAndTenant(user, out Guid userId, out Guid tenantId))
        {
            return Results.Unauthorized();
        }

        DateTimeOffset graceEndsAt = DateTimeOffset.UtcNow.AddDays(options.Value.ErasureGraceDays);
        ErasureRequestDto dto = await erasureRepo.CreateAsync(tenantId, userId, graceEndsAt, ct)
            .ConfigureAwait(false);

        return Results.Accepted($"/api/v1/gdpr/erasure/{dto.Id}", dto);
    }

    private static async Task<IResult> CancelErasureAsync(
        Guid id,
        ClaimsPrincipal user,
        IGdprErasureRepository erasureRepo,
        CancellationToken ct)
    {
        // C-7: fetch the erasure request first so we can verify it belongs to the caller's tenant.
        // An admin of tenant A must not be able to cancel tenant B's pending erasure.
        ErasureRequestDto? request = await erasureRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (request is null)
        {
            return Results.NotFound();
        }

        // Platform-wide admins (no tenant_id claim) may cancel any tenant's erasure.
        string? callerTenantIdRaw = user.FindFirstValue("tenant_id");
        if (callerTenantIdRaw is not null)
        {
            if (!Guid.TryParse(callerTenantIdRaw, out Guid callerTenantId)
                || callerTenantId != request.TenantId)
            {
                return Results.Forbid();
            }
        }

        bool cancelled = await erasureRepo.CancelAsync(id, ct).ConfigureAwait(false);
        return cancelled ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> RecordConsentAsync(
        RecordConsentRequest request,
        ClaimsPrincipal user,
        IGdprConsentRepository consentRepo,
        IValidator<RecordConsentCommand> validator,
        CancellationToken ct)
    {
        if (!TryGetUserAndTenant(user, out Guid userId, out Guid tenantId))
        {
            return Results.Unauthorized();
        }

        var command = new RecordConsentCommand(tenantId, userId, request.ConsentKey, request.Granted, request.Version);

        FluentValidation.Results.ValidationResult validation = await validator.ValidateAsync(command, ct)
            .ConfigureAwait(false);

        if (!validation.IsValid)
        {
            return Results.ValidationProblem(validation.ToDictionary());
        }

        await consentRepo.AppendAsync(tenantId, userId, request.ConsentKey, request.Granted, request.Version, ct)
            .ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> GetLatestConsentAsync(
        string key,
        ClaimsPrincipal user,
        IGdprConsentRepository consentRepo,
        CancellationToken ct)
    {
        if (!TryGetUserAndTenant(user, out Guid userId, out Guid tenantId))
        {
            return Results.Unauthorized();
        }

        ConsentDto? consent = await consentRepo.GetLatestAsync(tenantId, userId, key, ct)
            .ConfigureAwait(false);

        return consent is null ? Results.NotFound() : Results.Ok(consent);
    }

    private static Task<IResult> GetDpaTemplateAsync()
    {
        string? template = ReadEmbeddedResource("Gdpr.Api.Resources.dpa-template.md");
        if (template is null)
        {
            return Task.FromResult(Results.Problem("DPA template not found.", statusCode: 500));
        }

        return Task.FromResult(Results.Content(template, "text/markdown"));
    }

    private static async Task<IResult> GetSubProcessorsAsync(
        IGdprSubProcessorRepository repo,
        CancellationToken ct)
    {
        var list = await repo.GetAllAsync(ct).ConfigureAwait(false);
        return Results.Ok(list);
    }

    private static async Task<IResult> CreateSubProcessorAsync(
        CreateSubProcessorRequest request,
        IGdprSubProcessorRepository repo,
        CancellationToken ct)
    {
        SubProcessorDto dto = await repo.CreateAsync(
            request.Name, request.Country, request.Purpose, request.DataTypes, request.Website, ct)
            .ConfigureAwait(false);

        return Results.Created($"/api/v1/gdpr/sub-processors/{dto.Id}", dto);
    }

    private static async Task<IResult> DeleteSubProcessorAsync(
        Guid id,
        IGdprSubProcessorRepository repo,
        CancellationToken ct)
    {
        bool deleted = await repo.DeleteAsync(id, ct).ConfigureAwait(false);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static bool TryGetUserAndTenant(ClaimsPrincipal user, out Guid userId, out Guid tenantId)
    {
        userId = Guid.Empty;
        tenantId = Guid.Empty;

        string? sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        string? tid = user.FindFirstValue("tenant_id");

        if (sub is null || tid is null)
        {
            return false;
        }

        return Guid.TryParse(sub, out userId) && Guid.TryParse(tid, out tenantId);
    }

    private static string? ReadEmbeddedResource(string resourceName)
    {
        Assembly asm = typeof(GdprModule).Assembly;
        using Stream? stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>Request body for recording consent.</summary>
public sealed record RecordConsentRequest(string ConsentKey, bool Granted, string Version);

/// <summary>Request body for creating a sub-processor.</summary>
public sealed record CreateSubProcessorRequest(
    string Name,
    string Country,
    string Purpose,
    string DataTypes,
    string Website);
