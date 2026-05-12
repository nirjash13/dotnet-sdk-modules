using System;
using System.Security.Cryptography.X509Certificates;
using FluentValidation;
using Identity.Application.ApiKeys;
using Identity.Application.Auth;
using Identity.Application.Authorization;
using Identity.Application.Impersonation;
using Identity.Application.Lifecycle;
using Identity.Application.Mfa;
using Identity.Application.Organizations;
using Identity.Application.Services;
using Identity.Application.Social;
using Identity.Infrastructure.ApiKeys;
using Identity.Infrastructure.Auth;
using Identity.Infrastructure.Authorization;
using Identity.Infrastructure.Certificates;
using Identity.Infrastructure.Claims;
using Identity.Infrastructure.DomainClaims;
using Identity.Infrastructure.Impersonation;
using Identity.Infrastructure.Jobs;
using Identity.Infrastructure.Mfa;
using Identity.Infrastructure.Notifications;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Seeding;
using Identity.Infrastructure.SocialLogin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using SaasBuilder.Persistence;

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
        // 1. EF Core DbContext via SaasBuilder persistence helper (adds interceptor + accessor).
        services.AddSaasBuilderPersistence<IdentityDbContext>(options =>
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

        // Phase 2 — Organizations & RBAC repositories.
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();

        // Phase 2 — Organization command handlers.
        services.AddScoped<CreateOrganizationHandler>();
        services.AddScoped<InviteMemberHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<ChangeMemberRoleHandler>();
        services.AddScoped<RemoveMemberHandler>();
        services.AddScoped<TransferOwnershipHandler>();

        // Phase 2 — Permission system.
        services.AddSingleton<IPermissionDefinitionProvider, BuiltinPermissionDefinitionProvider>();
        services.AddScoped<IPermissionRegistry, PermissionRegistry>();

        // FluentValidation validators for organization commands.
        // Note: RequiresPermissionAuthorizationHandler is registered in the API layer (IdentityModule)
        // to keep the IAuthorizationHandler dependency out of Infrastructure.
        services.AddValidatorsFromAssemblyContaining<CreateOrganizationValidator>();

        // Phase 2 — Auth flows: Argon2id, email verification, password reset, lockout.
        services.AddSingleton<IArgon2idHasher, Argon2idPasswordHasher>();
        services.AddScoped<IEmailVerificationTokenStore, EmailVerificationTokenStore>();
        services.AddScoped<IPasswordResetTokenStore, PasswordResetTokenStore>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IAccountLockoutService, AccountLockoutService>();
        services.Configure<LockoutOptions>(configuration.GetSection("Identity:Lockout"));

        // Phase 2 — TOTP MFA.
        services.AddScoped<ITotpCredentialStore, TotpCredentialStore>();
        services.AddScoped<ITotpService, TotpService>();

        // Phase 2 — API keys.
        services.AddScoped<IApiKeyStore, ApiKeyStore>();
        services.AddScoped<IApiKeyService, ApiKeyService>();

        // Phase 2 — Impersonation.
        services.AddScoped<IImpersonationSessionStore, ImpersonationSessionStore>();
        services.AddScoped<IImpersonationService, ImpersonationService>();

        // Phase 2 — Social login adapters (scaffold).
        services.AddSingleton<ISocialLoginAdapter, GoogleProvider>();
        services.AddSingleton<ISocialLoginAdapter, MicrosoftProvider>();
        services.AddSingleton<ISocialLoginAdapter, GitHubProvider>();
        services.AddSingleton<ISocialLoginAdapter, AppleProvider>();

        // Phase 2.4 — Domain-claimed organizations.
        services.AddScoped<IOrganizationDomainClaimRepository, OrganizationDomainClaimRepository>();
        services.AddScoped<ClaimDomainHandler>();
        services.AddScoped<VerifyDomainClaimHandler>();
        services.AddScoped<DeleteDomainClaimHandler>();

        // Phase 2.4 — Domain ownership verifier.
        // NoopDomainOwnershipVerifier is only used when BOTH IsDevelopment AND AutoVerify=true.
        // A lone AutoVerify=true in a production appsettings does NOT bypass verification.
        //
        // TODO(C-14): DnsTxtDomainOwnershipVerifier lacks DNSSEC AD-bit validation.
        // Until a DNSSEC-validating implementation is wired, restrict the DNS verifier to
        // Development (or explicitly acknowledged insecure deployments). In production, callers
        // must set INSECURE_DNS_ACK=true to opt in and accept the risk.
        bool autoVerify = configuration.GetValue<bool>("Identity:DomainClaims:AutoVerify");
        bool insecureDnsAck = string.Equals(
            Environment.GetEnvironmentVariable("INSECURE_DNS_ACK"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (environment.IsDevelopment() && autoVerify)
        {
            services.AddScoped<IDomainOwnershipVerifier, NoopDomainOwnershipVerifier>();
        }
        else if (environment.IsDevelopment() || insecureDnsAck)
        {
            // DNS verifier active but AD-bit validation is NOT enforced — log a startup warning.
            services.AddScoped<IDomainOwnershipVerifier, DnsTxtDomainOwnershipVerifier>();
            // Warning is emitted from DnsTxtDomainOwnershipVerifier constructor (see C-14 comment).
        }
        else
        {
            // Production without explicit opt-in: fail fast rather than silently accept spoofable DNS.
            throw new InvalidOperationException(
                "DnsTxtDomainOwnershipVerifier is not production-safe without DNSSEC AD-bit validation. " +
                "Set INSECURE_DNS_ACK=true to acknowledge and continue, or provide a DNSSEC-validating verifier. (C-14)");
        }

        // Phase 2.11 — Account deletion grace period.
        services.AddScoped<IAccountRestoreTokenStore, AccountRestoreTokenStore>();
        services.AddScoped<IUserTombstoneRepository, UserTombstoneRepository>();
        services.AddScoped<RequestAccountDeletionHandler>();
        services.AddScoped<RestoreAccountHandler>();
        services.AddScoped<HardDeleteExpiredAccountsHandler>();

        // Phase 2.11 — Notification dispatcher: logging-only by default; host can override.
        services.AddScoped<INotificationDispatcherAdapter, LoggingOnlyNotificationDispatcherAdapter>();

        // Phase 2.11 — Hangfire recurring job (registered only if Hangfire is available).
        RegisterHardDeleteJobIfHangfirePresent(services);

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
    /// Registers the <see cref="HardDeleteExpiredAccountsJob"/> Hangfire recurring job
    /// if Hangfire's <c>RecurringJob</c> type is available at runtime.
    /// Skips silently with a log warning if Hangfire is not in the assembly graph.
    /// </summary>
    private static void RegisterHardDeleteJobIfHangfirePresent(IServiceCollection services)
    {
        services.AddSingleton<HardDeleteExpiredAccountsJob>();

        // Use a hosted service to schedule the recurring job after the DI container is built.
        services.AddHostedService<HardDeleteJobScheduler>();
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
