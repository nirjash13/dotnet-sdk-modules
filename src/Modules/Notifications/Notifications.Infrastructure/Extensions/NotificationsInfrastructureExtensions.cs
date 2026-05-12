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
/// Provider is selected by <c>Notifications:Email:Provider</c> config key:
/// <c>SendGrid | AwsSes | Postmark | Smtp | NoOp</c>. Default is NoOp + warning log.
/// </summary>
public static class NotificationsInfrastructureExtensions
{
    /// <summary>Registers all infrastructure services for the Notifications module.</summary>
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

        // 4. Email dispatcher — selected by Notifications:Email:Provider.
        string provider = configuration["Notifications:Email:Provider"] ?? string.Empty;

        switch (provider.Trim().ToUpperInvariant())
        {
            case "SENDGRID":
                services.Configure<SendGridOptions>(configuration.GetSection(SendGridOptions.SectionName));
                services.AddScoped<INotificationDispatcher, SendGridEmailDispatcher>();
                break;

            case "AWSSES":
                services.Configure<AwsSesOptions>(configuration.GetSection(AwsSesOptions.SectionName));
                services.AddScoped<INotificationDispatcher, AwsSesEmailDispatcher>();
                break;

            case "POSTMARK":
                services.Configure<PostmarkOptions>(configuration.GetSection(PostmarkOptions.SectionName));
                services.AddScoped<INotificationDispatcher, PostmarkEmailDispatcher>();
                break;

            case "SMTP":
                SmtpOptions smtpOptions = new SmtpOptions();
                configuration.GetSection(SmtpOptions.SectionName).Bind(smtpOptions);

                if (!string.IsNullOrWhiteSpace(smtpOptions.Host))
                {
                    services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
                    services.AddScoped<INotificationDispatcher, SmtpEmailNotificationDispatcher>();
                }
                else
                {
                    RegisterNoOp(services, "SMTP requested but Notifications:Smtp:Host is not configured.");
                }

                break;

            default:
                // Legacy fallback: check for bare SMTP config (pre-provider-selection behaviour).
                SmtpOptions legacySmtp = new SmtpOptions();
                configuration.GetSection(SmtpOptions.SectionName).Bind(legacySmtp);

                if (!string.IsNullOrWhiteSpace(legacySmtp.Host))
                {
                    services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
                    services.AddScoped<INotificationDispatcher, SmtpEmailNotificationDispatcher>();
                }
                else
                {
                    string reason = string.IsNullOrWhiteSpace(provider)
                        ? "Notifications:Email:Provider is not set."
                        : $"Unknown provider '{provider}'.";
                    RegisterNoOp(services, reason);
                }

                break;
        }

        // 5. SMS dispatcher — Twilio if configured, otherwise no-op SMS.
        TwilioOptions twilioOptions = new TwilioOptions();
        configuration.GetSection(TwilioOptions.SectionName).Bind(twilioOptions);

        if (!string.IsNullOrWhiteSpace(twilioOptions.AccountSid) &&
            !string.IsNullOrWhiteSpace(twilioOptions.AuthToken))
        {
            services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
            // M-O1 fix: register as INotificationDispatcher so the SMS channel is reachable.
            services.AddScoped<TwilioSmsDispatcher>();
            services.AddScoped<INotificationDispatcher, TwilioSmsDispatcher>();
        }

        return services;
    }

    private static void RegisterNoOp(IServiceCollection services, string reason)
    {
        services.AddScoped<INotificationDispatcher>(sp =>
        {
            ILogger<NoOpNotificationDispatcher> logger =
                sp.GetRequiredService<ILogger<NoOpNotificationDispatcher>>();
            logger.LogWarning(
                "Notifications module: registering NoOpNotificationDispatcher — notifications will not be delivered. Reason: {Reason}",
                reason);
            return new NoOpNotificationDispatcher(logger);
        });
    }
}
