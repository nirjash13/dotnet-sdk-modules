using System.Linq;
using System.Security.Claims;
using Chassis.SharedKernel.Abstractions;
using Identity.Contracts;
using Identity.Infrastructure.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the Identity module.
/// Discovered by <c>ReflectionModuleLoader</c> at startup via assembly scan.
/// </summary>
/// <remarks>
/// <para>
/// <b>Services registered:</b> OpenIddict server + EF Core stores, claim enrichment,
/// certificate loading, dev client seeder.
/// </para>
/// <para>
/// <b>OpenIddict endpoint behaviour:</b>
/// OpenIddict auto-maps <c>/connect/token</c>, <c>/connect/authorize</c>,
/// <c>/connect/revoke</c>, <c>/connect/introspect</c>, and <c>/.well-known/openid-configuration</c>
/// via passthrough mode. No explicit MapPost/MapGet calls are needed for those.
/// </para>
/// </remarks>
public sealed class IdentityModule : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Resolve IHostEnvironment from the existing registrations.
        // IHostEnvironment is registered as a singleton by WebApplication.CreateBuilder()
        // before any module ConfigureServices calls are made, so this is safe.
        // We build a temporary scope here only to read IHostEnvironment, not to resolve
        // request-scoped services — suppress the ServiceLocator anti-pattern warning.
#pragma warning disable ASP0000 // Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'
        using ServiceProvider sp = services.BuildServiceProvider();
        IHostEnvironment environment = sp.GetRequiredService<IHostEnvironment>();
#pragma warning restore ASP0000

        services.AddIdentityInfrastructure(config, environment);
    }

    /// <inheritdoc />
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        // OpenIddict auto-maps all OIDC protocol endpoints when passthrough is enabled.
        // Map only application-specific endpoints here.
        RouteGroupBuilder identity = endpoints
            .MapGroup("/api/v1/identity")
            .RequireAuthorization()
            .WithTags("identity");

        identity.MapGet("/me", GetCurrentUser)
            .WithName("Identity_GetCurrentUser")
            .WithSummary("Returns the authenticated principal's identity claims.");
    }

    private static IResult GetCurrentUser(HttpContext context)
    {
        System.Security.Claims.ClaimsPrincipal user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        ClaimsPrincipalDto dto = new ClaimsPrincipalDto
        {
            Sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub"),
            TenantId = user.FindFirstValue("tenant_id"),
            Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            Name = user.FindFirstValue(ClaimTypes.Name),
        };

        return Results.Ok(dto);
    }
}
