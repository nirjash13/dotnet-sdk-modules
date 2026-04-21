using System;
using Chassis.Host.Pipeline;
using Identity.Application.Consumers;
using Ledger.Application.Commands;
using Ledger.Application.Consumers;
using Ledger.Application.Queries;
using Ledger.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Registration.Application.Sagas;
using Registration.Infrastructure.Persistence;
using Reporting.Application.Consumers;

namespace Chassis.Host.Transport;

/// <summary>
/// MassTransit wiring for in-process mediator and RabbitMQ bus modes.
/// </summary>
internal static class MassTransitConfig
{
    /// <summary>
    /// Adds MassTransit Mediator with the chassis filter pipeline.
    /// </summary>
    public static void AddChassisMediator(IServiceCollection services)
    {
        services.AddMediator(configurator =>
        {
            // Ledger request/response handlers.
            configurator.AddConsumer<PostTransactionHandler>();
            configurator.AddConsumer<GetAccountBalanceHandler>();

            // Registration/identity/reporting consumers used by saga workflows.
            configurator.AddConsumer<CreateUserCommandConsumer>();
            configurator.AddConsumer<DeleteUserCommandConsumer>();
            configurator.AddConsumer<InitLedgerCommandConsumer>();
            configurator.AddConsumer<RollbackLedgerInitCommandConsumer>();
            configurator.AddConsumer<ProvisionReportingCommandConsumer>();
            configurator.AddConsumer<UnprovisionReportingCommandConsumer>();
            configurator.AddConsumer<LedgerTransactionPostedConsumer>();

            configurator.ConfigureMediator((ctx, cfg) =>
            {
                cfg.UseSendFilter(typeof(TenantPropagationSendFilter<>), ctx);
                cfg.UsePublishFilter(typeof(PublishTenantPropagationFilter<>), ctx);

                cfg.UseConsumeFilter(typeof(TenantPropagationConsumeFilter<>), ctx);
                cfg.UseConsumeFilter(typeof(LoggingFilter<>), ctx);
                cfg.UseConsumeFilter(typeof(TenantFilter<>), ctx);
                cfg.UseConsumeFilter(typeof(ValidationFilter<>), ctx);
            });
        });
    }

    /// <summary>
    /// Adds MassTransit RabbitMQ bus mode with EF outbox and configured consumers.
    /// </summary>
    public static void AddChassisBus(IServiceCollection services, IConfiguration config)
    {
        string rabbitHost = config["RabbitMq:Host"] ?? "localhost";
        string rabbitUser = config["RabbitMq:Username"] ?? "guest";
        string rabbitPass = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            x.AddSagaStateMachine<RegistrationSaga, RegistrationSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<RegistrationDbContext>();
                    r.UsePostgres();
                });

            x.AddConsumer<CreateUserCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<DeleteUserCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<PostTransactionHandler>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<GetAccountBalanceHandler>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<InitLedgerCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<RollbackLedgerInitCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<ProvisionReportingCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<UnprovisionReportingCommandConsumer>(cfg =>
                cfg.UseMessageRetry(r => r.Immediate(3)));
            x.AddConsumer<LedgerTransactionPostedConsumer>(cfg =>
            {
                cfg.UseMessageRetry(r =>
                    r.Exponential(
                        5,
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromSeconds(1)));

                cfg.UseScheduledRedelivery(r =>
                    r.Intervals(
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(30),
                        TimeSpan.FromHours(2)));
            });

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.UseSendFilter(typeof(TenantPropagationSendFilter<>), ctx);
                cfg.UsePublishFilter(typeof(PublishTenantPropagationFilter<>), ctx);

                cfg.UseConsumeFilter(typeof(TenantPropagationConsumeFilter<>), ctx);
                cfg.UseConsumeFilter(typeof(LoggingFilter<>), ctx);
                cfg.UseConsumeFilter(typeof(TenantFilter<>), ctx);
                cfg.UseConsumeFilter(typeof(ValidationFilter<>), ctx);

                cfg.ConfigureEndpoints(ctx);
            });
        });
    }
}
