using System;
using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Infrastructure.Jobs;
using Billing.Infrastructure.Persistence;
using Billing.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Persistence;

namespace Billing.Infrastructure.Extensions;

/// <summary>
/// Extension methods that wire up the Billing module's infrastructure services.
/// </summary>
public static class BillingInfrastructureExtensions
{
    /// <summary>
    /// Registers all infrastructure services required by the Billing module.
    /// </summary>
    public static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'SaasBuilder' (or fallback 'DefaultConnection') is not configured.");

        // 1. EF Core DbContext.
        services.AddSaasBuilderPersistence<BillingDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "billing");
                });
        });

        // 2. Repositories.
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();

        // 3. Application handlers.
        services.AddScoped<CreateSubscriptionHandler>();
        services.AddScoped<CancelSubscriptionHandler>();
        services.AddScoped<UpgradePlanHandler>();
        services.AddScoped<PauseResumeSubscriptionHandler>();

        // 4. Provider stubs — default to Stripe (throws NotImplementedException until Phase 4 integration).
        // Consumer can call services.AddSingleton<IBillingProvider, MyProvider>() after this call
        // to override the default.
        services.AddSingleton<IBillingProvider, StripeBillingProvider>();

        // 5. Webhook signature verifiers (keyed by provider name).
        services.AddSingleton<IWebhookSignatureVerifier, StripeWebhookSignatureVerifier>();

        // 6. Daily reconciliation background service.
        services.AddHostedService<DailyReconciliationJob>();

        return services;
    }
}
