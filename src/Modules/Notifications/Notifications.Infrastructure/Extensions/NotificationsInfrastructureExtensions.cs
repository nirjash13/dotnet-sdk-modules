using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Infrastructure.Dispatchers;
using Notifications.Infrastructure.Options;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Templating;
using SaasBuilder.Persistence;

namespace Notifications.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Notifications module infrastructure services.
/// Implements the silent-degradation pattern: missing SMTP config registers the no-op dispatcher.
/// </summary>
public static class NotificationsInfrastructureExtensions
{
    /// <summary>
    /// Registers all infrastructure services for the Notifications module.
    /// </summary>
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Database connection — fall back to shared connection string.
        string? connectionString = configuration.GetConnectionString("SaasBuilder")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (connectionString is not null)
        {
            services.AddSaasBuilderPersistence<NotificationsDbContext>(options =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(NotificationsDbContext).Assembly.FullName);
                        npgsql.MigrationsHistoryTable("__ef_migrations_history", "notifications");
                    });
            });
        }

        // 2. In-app store (only if DB is configured).
        if (connectionString is not null)
        {
            services.AddScoped<IInAppNotificationStore, EfCoreInAppNotificationStore>();
        }

        // 3. Template renderer.
        services.AddSingleton<INotificationTemplate, FallbackNotificationTemplate>();

        // 4. Dispatcher — SMTP if configured, no-op otherwise.
        SmtpOptions smtpOptions = new SmtpOptions();
        configuration.GetSection(SmtpOptions.SectionName).Bind(smtpOptions);

        if (!string.IsNullOrWhiteSpace(smtpOptions.Host))
        {
            services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
            services.AddScoped<INotificationDispatcher, SmtpEmailNotificationDispatcher>();
        }
        else
        {
            // Silent degradation: log at service registration time using the service provider build callback.
            services.AddScoped<INotificationDispatcher>(sp =>
            {
                ILogger<NoOpNotificationDispatcher> logger =
                    sp.GetRequiredService<ILogger<NoOpNotificationDispatcher>>();
                logger.LogWarning(
                    "Notifications module: '{SectionName}:Host' is not configured. " +
                    "Registering NoOpNotificationDispatcher — notifications will not be delivered. " +
                    "Set the SMTP host via environment variable or appsettings.",
                    SmtpOptions.SectionName);
                return new NoOpNotificationDispatcher(logger);
            });
        }

        return services;
    }
}
