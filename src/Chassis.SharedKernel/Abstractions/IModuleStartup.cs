// IEndpointRouteBuilder is ASP.NET Core-specific and does not exist in netstandard2.0.
// The Configure(IEndpointRouteBuilder) method is therefore conditionally compiled.
// .NET Framework 4.x / .NET Core 6/8 consumers get only ConfigureServices.

#if NET10_0_OR_GREATER
using Microsoft.AspNetCore.Routing;
#endif

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chassis.SharedKernel.Abstractions;

/// <summary>
/// Contract that every chassis module must implement to participate in host startup.
/// The host discovers all types implementing this interface via assembly scan
/// (see <c>Chassis.Host.Modules.IModuleLoader</c>) and calls both lifecycle methods.
/// </summary>
public interface IModuleStartup
{
    /// <summary>
    /// Register the module's services into the dependency injection container.
    /// Called during <c>WebApplication.CreateBuilder()</c> / <c>IHostBuilder.ConfigureServices()</c>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="config">The host configuration (environment variables, appsettings, etc.).</param>
    void ConfigureServices(IServiceCollection services, IConfiguration config);

#if NET10_0_OR_GREATER
    /// <summary>
    /// Map the module's HTTP endpoints using Minimal API or controller conventions.
    /// Called after <c>app.UseRouting()</c> and <c>app.UseAuthorization()</c>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    void Configure(IEndpointRouteBuilder endpoints);
#endif
}
