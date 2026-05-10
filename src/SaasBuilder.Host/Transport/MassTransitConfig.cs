using System;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.Host.Pipeline;

namespace SaasBuilder.Host.Transport;

/// <summary>
/// MassTransit wiring for in-process mediator and RabbitMQ bus modes.
/// Consumer registration is delegated to the host application via the
/// <paramref name="registerConsumers"/> callback so the library has no
/// compile-time dependency on module assemblies.
/// </summary>
internal static class MassTransitConfig
{
    /// <summary>
    /// Adds MassTransit Mediator with the chassis filter pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="registerConsumers">
    /// Callback invoked on the <see cref="IMediatorRegistrationConfigurator"/> so
    /// the host application can register module consumers without the library
    /// needing to reference module assemblies.
    /// When <see langword="null"/> no consumers are registered (useful for tests).
    /// </param>
    public static void AddSaasBuilderMediator(
        IServiceCollection services,
        Action<IMediatorRegistrationConfigurator>? registerConsumers = null)
    {
        services.AddMediator(configurator =>
        {
            registerConsumers?.Invoke(configurator);

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
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">The host configuration (connection strings, etc.).</param>
    /// <param name="registerConsumers">
    /// Callback invoked on the <see cref="IBusRegistrationConfigurator"/> so
    /// the host application can register module consumers and sagas without the
    /// library needing to reference module assemblies.
    /// When <see langword="null"/> no consumers are registered (useful for tests).
    /// </param>
    public static void AddSaasBuilderBus(
        IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? registerConsumers = null)
    {
        string rabbitHost = config["RabbitMq:Host"] ?? "localhost";
        string rabbitUser = config["RabbitMq:Username"] ?? "guest";
        string rabbitPass = config["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            registerConsumers?.Invoke(x);

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
