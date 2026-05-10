using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlags.Application.Abstractions;
using FeatureFlags.Contracts;
using FeatureFlags.Infrastructure.Extensions;
using FeatureFlags.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace FeatureFlags.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the FeatureFlags module.
/// </summary>
public sealed class FeatureFlagsModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddFeatureFlagsInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder flags = endpoints
            .MapGroup("/api/v1/flags")
            .WithTags("feature-flags")
            .RequireAuthorization();

        // GET /api/v1/flags/{flagKey} — evaluate flag for current tenant (admin-visible)
        flags.MapGet("/{flagKey}", EvaluateFlagAsync)
            .WithName("FeatureFlags_EvaluateFlag")
            .WithSummary("Evaluates a feature flag for the current tenant.");

        // PUT /api/v1/admin/flags/{flagKey}/tenants/{tenantId} — admin override
        endpoints
            .MapGroup("/api/v1/admin/flags")
            .WithTags("feature-flags-admin")
            .RequireAuthorization("admin")
            .MapPut("/{flagKey}/tenants/{tenantId:guid}", SetTenantOverrideAsync)
            .WithName("FeatureFlags_SetTenantOverride")
            .WithSummary("Sets a per-tenant feature flag override (admin only).");
    }

    private static async Task<IResult> EvaluateFlagAsync(
        string flagKey,
        IFeatureClient client,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        bool value = await client.GetBooleanValueAsync(flagKey, false, ct: ct).ConfigureAwait(false);
        return Results.Ok(new { FlagKey = flagKey, Value = value, TenantId = tenant.TenantId });
    }

    private static async Task<IResult> SetTenantOverrideAsync(
        string flagKey,
        Guid tenantId,
        TenantFlagOverrideRequest request,
        FeatureFlagsDbContext db,
        CancellationToken ct)
    {
        FeatureFlag? flag = await db.FeatureFlags
            .FirstOrDefaultAsync(f => f.Key == flagKey, ct)
            .ConfigureAwait(false);

        if (flag is null)
        {
            return Results.NotFound(new { Detail = $"Flag '{flagKey}' not found." });
        }

        TenantFlagOverride? existing = await db.TenantFlagOverrides
            .FirstOrDefaultAsync(o => o.FeatureFlagId == flag.Id && o.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Remove and re-add to update the value (owned entity pattern).
            db.TenantFlagOverrides.Remove(existing);
        }

        db.TenantFlagOverrides.Add(TenantFlagOverride.Create(flag.Id, tenantId, request.Value));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Results.Ok(new { FlagKey = flagKey, TenantId = tenantId, Value = request.Value });
    }

    /// <summary>Request body for the PUT tenant override endpoint.</summary>
    public sealed class TenantFlagOverrideRequest
    {
        /// <summary>Gets or sets the override value to apply.</summary>
        public bool Value { get; set; }
    }
}
