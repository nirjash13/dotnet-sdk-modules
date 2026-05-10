using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.SharedKernel.Tenancy.Resolution;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Configures which <see cref="ITenantResolver"/> implementations participate in the
/// tenant resolution pipeline and in what priority order.
/// </summary>
public sealed class TenantResolverOptions
{
    private readonly List<(Type ResolverType, int Priority)> _resolvers =
        new List<(Type ResolverType, int Priority)>();

    /// <summary>
    /// Gets the registered resolver types and their priorities.
    /// The list is consumed by the DI extension to register resolvers as scoped services.
    /// </summary>
    internal IReadOnlyList<(Type ResolverType, int Priority)> Resolvers => _resolvers;

    /// <summary>
    /// Registers a resolver of type <typeparamref name="TResolver"/> with the specified priority.
    /// Resolvers with higher priority values are tried first. When two resolvers share the same
    /// priority, they are evaluated in registration order.
    /// </summary>
    /// <typeparam name="TResolver">The resolver type to register. Must implement <see cref="ITenantResolver"/>.</typeparam>
    /// <param name="priority">The evaluation priority (higher = earlier).</param>
    /// <returns>This options instance for fluent chaining.</returns>
    public TenantResolverOptions Use<TResolver>(int priority)
        where TResolver : class, ITenantResolver
    {
        _resolvers.Add((typeof(TResolver), priority));
        return this;
    }

    /// <summary>
    /// Registers all resolver types in their default priority order.
    /// Called internally when the caller has not configured resolvers explicitly.
    /// </summary>
    internal void UseDefaults(IServiceCollection services)
    {
        // If the caller hasn't explicitly registered any resolvers, use the defaults.
        // Default registration order mirrors priority (highest first) for readability.
        if (_resolvers.Count == 0)
        {
            _resolvers.Add((typeof(Tenancy.Resolution.JwtClaimTenantResolver), 100));
            _resolvers.Add((typeof(Tenancy.Resolution.HeaderTenantResolver), 50));
            _resolvers.Add((typeof(Tenancy.Resolution.PathTenantResolver), 30));
            _resolvers.Add((typeof(Tenancy.Resolution.SubdomainTenantResolver), 20));
        }

        // Register each resolver as scoped (request-lifetime) implementing the interface.
        // The middleware resolves IEnumerable<ITenantResolver> to get all of them.
        foreach ((Type resolverType, _) in _resolvers)
        {
            services.AddScoped(typeof(ITenantResolver), resolverType);
        }
    }
}
