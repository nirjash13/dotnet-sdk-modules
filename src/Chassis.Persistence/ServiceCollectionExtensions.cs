using System;
using Chassis.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chassis.Persistence;

/// <summary>
/// Extension methods for registering Chassis persistence infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the given <typeparamref name="TContext"/> along with the
    /// <see cref="TenantCommandInterceptor"/> and <see cref="ITenantContextAccessor"/>
    /// singleton in the DI container.
    /// </summary>
    /// <typeparam name="TContext">
    /// A concrete <see cref="ChassisDbContext"/> subclass for a specific bounded context.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional additional EF Core options configuration (e.g. connection string,
    /// migration assembly). The interceptor is always added regardless.
    /// </param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddChassisPersistence<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? configure = null)
        where TContext : ChassisDbContext
    {
        // Register the accessor as a singleton so it is shared across all scoped services
        // within a request. The TenantMiddleware and pipeline filters write to it; the
        // DbContext reads from it.
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // The interceptor is registered as scoped to match the DbContext lifetime.
        // It holds a per-instance last-set-tenant cache so it must not be a singleton.
        services.AddScoped<TenantCommandInterceptor>();

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            // Apply the interceptor before delegating to caller-supplied configuration
            // so the caller cannot accidentally clear it.
            TenantCommandInterceptor interceptor =
                serviceProvider.GetRequiredService<TenantCommandInterceptor>();
            options.AddInterceptors(interceptor);

            configure?.Invoke(options);
        });

        return services;
    }
}
