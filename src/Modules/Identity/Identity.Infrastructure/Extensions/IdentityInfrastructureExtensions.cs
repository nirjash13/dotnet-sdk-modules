using System;
using System.Security.Cryptography.X509Certificates;
using Chassis.Persistence;
using Identity.Application.Services;
using Identity.Infrastructure.Certificates;
using Identity.Infrastructure.Claims;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace Identity.Infrastructure.Extensions;

/// <summary>
/// Extension methods that wire up the Identity module's infrastructure services:
/// EF Core, OpenIddict server, claim enrichment, certificate loading, and dev seeding.
/// </summary>
public static class IdentityInfrastructureExtensions
{
    /// <summary>
    /// Registers all infrastructure services required by the Identity module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="environment">The host environment (selects dev vs prod cert strategy).</param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // 1. EF Core DbContext via Chassis persistence helper (adds interceptor + accessor).
        services.AddChassisPersistence<IdentityDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured. " +
                    "Set it via environment variable 'ConnectionStrings__DefaultConnection'.");

            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity");
                });
        });

        // 2. Application-layer services.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICertificateProvider, CertificateLoader>();
        services.AddScoped<ITenantClaimEnricher, TenantClaimEnricher>();

        // 3. Scoped OpenIddict sign-in event handler.
        services.AddScoped<TenantClaimEventHandler>();

        // 4. OpenIddict server, core, and validation.
        RegisterOpenIddict(services, configuration, environment);

        // 5. Dev client seeding (runs when Identity:SeedDevClient=true).
        services.AddHostedService<DevClientDataSeeder>();

        return services;
    }

    private static void RegisterOpenIddict(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        OpenIddictBuilder builder = services.AddOpenIddict();

        builder.AddCore(core =>
        {
            core.UseEntityFrameworkCore()
                .UseDbContext<IdentityDbContext>();
        });

        builder.AddServer(server =>
        {
            server
                .AllowAuthorizationCodeFlow()
                .AllowClientCredentialsFlow()
                .AllowRefreshTokenFlow()
                .RequireProofKeyForCodeExchange();

            server
                .SetTokenEndpointUris("/connect/token")
                .SetAuthorizationEndpointUris("/connect/authorize")
                .SetRevocationEndpointUris("/connect/revoke")
                .SetIntrospectionEndpointUris("/connect/introspect")
                .SetEndSessionEndpointUris("/connect/logout");

            server.RegisterScopes(
                OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Roles,
                "tenant");

            // Force JWT access tokens (not reference tokens) — §13 decision point.
            server.DisableAccessTokenEncryption();

            bool isDevelopment = environment.IsDevelopment()
                || configuration.GetValue<bool>("Identity:AllowInsecureMetadata");

            if (isDevelopment)
            {
                server
                    .AddDevelopmentSigningCertificate()
                    .AddDevelopmentEncryptionCertificate();

                server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough()
                    .DisableTransportSecurityRequirement();
            }
            else
            {
                string signingThumbprint = configuration["Identity:Certificates:SigningThumbprint"]
                    ?? throw new InvalidOperationException(
                        "Identity:Certificates:SigningThumbprint must be set in production via environment variable.");

                string encryptionThumbprint = configuration["Identity:Certificates:EncryptionThumbprint"]
                    ?? throw new InvalidOperationException(
                        "Identity:Certificates:EncryptionThumbprint must be set in production via environment variable.");

                server
                    .AddSigningCertificate(LoadFromStore(signingThumbprint, "signing"))
                    .AddEncryptionCertificate(LoadFromStore(encryptionThumbprint, "encryption"));

                server.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
            }

            server.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.ProcessSignInContext>(
                evt => evt
                    .UseScopedHandler<TenantClaimEventHandler>()
                    .SetOrder(500));
        });

        builder.AddValidation(validation =>
        {
            validation.UseLocalServer();
            validation.UseAspNetCore();
        });
    }

    /// <summary>
    /// Loads an <see cref="X509Certificate2"/> from the OS certificate store by thumbprint.
    /// Tries CurrentUser\My then LocalMachine\My.
    /// </summary>
    private static X509Certificate2 LoadFromStore(string thumbprint, string purpose)
    {
        thumbprint = thumbprint
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        StoreLocation[] locations = new StoreLocation[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine };
        foreach (StoreLocation location in locations)
        {
            using X509Store store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            X509Certificate2Collection found = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                validOnly: true);

            if (found.Count > 0)
            {
                return found[0];
            }
        }

        throw new InvalidOperationException(
            $"Certificate with thumbprint '{thumbprint}' for purpose '{purpose}' was not found " +
            "in CurrentUser\\My or LocalMachine\\My certificate stores.");
    }
}
