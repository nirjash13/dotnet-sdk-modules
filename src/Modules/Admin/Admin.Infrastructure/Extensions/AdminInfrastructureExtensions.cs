using Admin.Application.Abstractions;
using Admin.Application.Handlers;
using Admin.Application.Validators;
using Admin.Infrastructure.Persistence;
using Admin.Infrastructure.Services;
using Audit.Infrastructure.Extensions;
using Billing.Infrastructure.Extensions;
using Entitlements.Infrastructure.Extensions;
using FeatureFlags.Infrastructure.Extensions;
using FluentValidation;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Persistence;
using Webhooks.Infrastructure.Extensions;

namespace Admin.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Admin module infrastructure and application services.
/// </summary>
public static class AdminInfrastructureExtensions
{
    /// <summary>Registers all Admin module services, DbContext, and authorization policy.</summary>
    public static IServiceCollection AddAdminInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (connectionString is not null)
        {
            // Admin module's own DbContext.
            services.AddSaasBuilderPersistence<AdminDbContext>(options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(AdminDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "admin");
                    });
            });

            // Register IdentityDbContext directly — AddIdentityInfrastructure requires IHostEnvironment
            // (OpenIddict cert setup), so we register only the DbContext here.
            // The full Identity module setup is done by IdentityModule.ConfigureServices.
            services.AddSaasBuilderPersistence<IdentityDbContext>(options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity");
                    });
            });
        }

        // Ensure cross-module dependencies are registered.
        // AddDbContext<T> and AddScoped<> are idempotent — no-op when already registered.
        services.AddBillingInfrastructure(configuration);
        services.AddEntitlementsInfrastructure(configuration);
        services.AddFeatureFlagsInfrastructure(configuration);
        services.AddAuditInfrastructure(configuration);
        services.AddWebhooksInfrastructure(configuration);

        // Application layer services.
        services.AddScoped<ListTenantsHandler>();
        services.AddScoped<GetTenantInspectorHandler>();
        services.AddScoped<OverrideEntitlementHandler>();
        services.AddScoped<OverrideFeatureFlagHandler>();
        services.AddScoped<ApproveAdminActionHandler>();

        // Infrastructure service implementations.
        services.AddScoped<ITenantDirectoryService, TenantDirectoryService>();
        services.AddScoped<IOpsHealthChecker, OpsHealthChecker>();
        services.AddScoped<IAdminActionAuditor, AdminActionAuditor>();
        services.AddScoped<IPendingAdminActionStore, PendingAdminActionStore>();
        services.AddScoped<IFeatureFlagOverrideStore, FeatureFlagOverrideStore>();

        // FluentValidation validators.
        services.AddValidatorsFromAssemblyContaining<OverrideEntitlementRequestValidator>(
            ServiceLifetime.Scoped,
            includeInternalTypes: true);

        // Authorization policy: system-admin role + MFA required.
        services.AddAuthorizationBuilder()
            .AddPolicy("SystemAdmin", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("roles", "system-admin");
                policy.RequireClaim("mfa_used", "true");
            });

        // Register the authorization handler for SystemAdminRequirement.
        services.AddScoped<IAuthorizationHandler, Authorization.SystemAdminAuthorizationHandler>();

        return services;
    }
}
