using System;
using Billing.Application.Abstractions;
using Billing.Application.Commands;
using Billing.Application.Jobs;
using Billing.Application.Options;
using Billing.Application.Services;
using Billing.Infrastructure.Consumers;
using Billing.Infrastructure.Jobs;
using Billing.Infrastructure.Persistence;
using Billing.Infrastructure.Providers;
using MassTransit;
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

        // 3b. Application services.
        services.AddScoped<SeatSyncService>();

        // 4. Provider — default to Stripe; consumer can override after this call.
        // Consumer can call services.AddSingleton<IBillingProvider, MyProvider>() after this call
        // to override the default.
        services.AddSingleton<IBillingProvider, StripeBillingProvider>();

        // 5. Webhook signature verifiers (keyed by provider name).
        services.AddSingleton<IWebhookSignatureVerifier, StripeWebhookSignatureVerifier>();

        // 6. Daily reconciliation background service.
        services.AddHostedService<DailyReconciliationJob>();

        // 7. Dunning options + grace-period scanner.
        services.Configure<BillingOptions>(configuration.GetSection(BillingOptions.SectionName));
        services.AddScoped<SuspendTenantForUnpaidInvoiceJob>();
        services.AddHostedService<DunningGraceHostedService>();

        return services;
    }

    /// <summary>
    /// Registers the Billing module's MassTransit consumers on a mediator configurator.
    /// Call this inside <c>AddSaasBuilderHost</c>'s <c>opts.Transport.MediatorConsumers</c> callback:
    /// <code>
    /// opts.Transport.MediatorConsumers = cfg => cfg.AddBillingSeatSyncConsumers();
    /// </code>
    /// These consumers forward <c>MemberAdded</c>/<c>MemberRemoved</c> integration events to
    /// <see cref="SeatSyncService"/> so that per-seat billing quantities are kept in sync.
    /// </summary>
    public static IMediatorRegistrationConfigurator AddBillingSeatSyncConsumers(
        this IMediatorRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<MemberAddedConsumer>();
        configurator.AddConsumer<MemberRemovedConsumer>();
        return configurator;
    }

    /// <summary>
    /// Registers the Billing module's MassTransit consumers on a bus configurator.
    /// Call this inside <c>AddSaasBuilderHost</c>'s <c>opts.Transport.BusConsumers</c> callback:
    /// <code>
    /// opts.Transport.BusConsumers = cfg => cfg.AddBillingSeatSyncConsumers();
    /// </code>
    /// </summary>
    public static IBusRegistrationConfigurator AddBillingSeatSyncConsumers(
        this IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<MemberAddedConsumer>();
        configurator.AddConsumer<MemberRemovedConsumer>();
        return configurator;
    }
}
