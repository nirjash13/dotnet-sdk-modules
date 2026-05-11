using Admin.Api.Endpoints;
using Admin.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Abstractions;

namespace Admin.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the Admin/Control-Plane module.
/// Discovered by <c>ReflectionModuleLoader</c> at startup via assembly scan.
/// </summary>
/// <remarks>
/// All admin endpoints are protected by the <c>SystemAdmin</c> authorization policy,
/// which requires both a <c>role=system-admin</c> claim and an <c>mfa_used=true</c> claim.
/// </remarks>
public sealed class AdminModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddAdminInfrastructure(config);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder admin = endpoints
            .MapGroup("/api/v1/admin")
            .WithTags("admin")
            .RequireAuthorization("SystemAdmin");

        TenantEndpoints.Map(admin);
        SupportActionsEndpoints.Map(admin);
        OverrideEndpoints.Map(admin);
        JobDashboardEndpoints.Map(admin);
        WebhookDashboardEndpoints.Map(admin);
        OpsHealthEndpoints.Map(admin);
        ApprovalEndpoints.Map(admin);
    }
}
