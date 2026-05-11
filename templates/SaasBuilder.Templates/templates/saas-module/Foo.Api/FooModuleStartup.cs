using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Modules;
using Foo.Infrastructure.Persistence;

namespace Foo.Api;

/// <summary>Module startup registration for Foo.</summary>
public sealed class FooModuleStartup : IModuleStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register module services here
        // services.AddDbContext<FooDbContext>(opts => ...);
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFooEndpoints();
    }
}
