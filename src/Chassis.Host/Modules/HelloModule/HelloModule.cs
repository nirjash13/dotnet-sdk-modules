using Chassis.SharedKernel.Abstractions;
using Chassis.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chassis.Host.Modules.HelloModule;

/// <summary>
/// A minimal demonstration module that proves the assembly-scan loader works end-to-end.
/// Registers one endpoint: <c>GET /hello</c> that returns the current tenant id.
/// </summary>
/// <remarks>
/// This module lives inside the Chassis.Host assembly for Phase 1.
/// In Phase 3+ each business module will be a separate project/assembly discovered
/// from the <c>modules/</c> sub-directory.
/// </remarks>
internal sealed class HelloModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Hello module has no additional services to register.
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        // Tenant is populated by TenantMiddleware, not auth — AllowAnonymous is correct here
        // because authentication is a stub in Phase 1. TenantMiddleware still enforces tenant
        // context and returns 401 when the X-Tenant-Id header is absent.
        endpoints.MapGet("/hello", (ITenantContextAccessor accessor) =>
        {
            string tenant = accessor.Current?.TenantId.ToString() ?? "none";
            return Results.Ok(new { module = "hello", tenant });
        })
        .WithName("Hello")
        .WithTags("hello")
        .AllowAnonymous();
    }
}
