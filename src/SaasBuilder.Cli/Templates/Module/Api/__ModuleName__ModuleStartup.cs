using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Modules;
using __ModuleName__.Infrastructure.Persistence;

namespace __ModuleName__.Api;

/// <summary>Module startup registration for __ModuleName__.</summary>
public sealed class __ModuleName__ModuleStartup : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register module services here
        // services.AddDbContext<__ModuleName__DbContext>(...);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Map module endpoints here
        // var group = endpoints.MapGroup("/api/v1/__module_route__").RequireAuthorization();
    }
}
