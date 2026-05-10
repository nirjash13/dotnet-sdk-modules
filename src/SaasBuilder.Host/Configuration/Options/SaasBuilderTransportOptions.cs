using System;
using MassTransit;
using SaasBuilder.SharedKernel.Configuration;

namespace SaasBuilder.Host.Configuration.Options;

/// <summary>
/// Options controlling the MassTransit transport selection and consumer registration.
/// </summary>
public sealed class SaasBuilderTransportOptions
{
    /// <summary>Gets the selected transport. Defaults to <see cref="SaasTransport.InProc"/>.</summary>
    public SaasTransport Transport { get; private set; } = SaasTransport.InProc;

    /// <summary>
    /// Gets the callback used to register consumers on the in-process mediator.
    /// Set via <see cref="WithMediatorConsumers"/>.
    /// </summary>
    internal Action<IMediatorRegistrationConfigurator>? MediatorConsumers { get; private set; }

    /// <summary>
    /// Gets the callback used to register consumers on the RabbitMQ bus.
    /// Set via <see cref="WithBusConsumers"/>.
    /// </summary>
    internal Action<IBusRegistrationConfigurator>? BusConsumers { get; private set; }

    /// <summary>
    /// Selects the in-process MassTransit Mediator transport (default).
    /// </summary>
    public SaasBuilderTransportOptions UseInProc()
    {
        Transport = SaasTransport.InProc;
        return this;
    }

    /// <summary>
    /// Selects the out-of-process RabbitMQ Bus transport with EF Core Outbox/Inbox.
    /// Requires <c>RabbitMq:Host</c>, <c>RabbitMq:Username</c>, and <c>RabbitMq:Password</c>
    /// in application configuration.
    /// </summary>
    public SaasBuilderTransportOptions UseBus()
    {
        Transport = SaasTransport.Bus;
        return this;
    }

    /// <summary>
    /// Registers module consumers on the in-process MassTransit Mediator.
    /// Call this from the host application (not from the library) to keep
    /// the library free of compile-time module assembly references.
    /// </summary>
    /// <param name="configure">
    /// Delegate that adds consumers to the mediator configurator.
    /// Multiple calls are composed — later calls append to earlier ones.
    /// </param>
    public SaasBuilderTransportOptions WithMediatorConsumers(
        Action<IMediatorRegistrationConfigurator> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        Action<IMediatorRegistrationConfigurator>? existing = MediatorConsumers;
        MediatorConsumers = existing is null
            ? configure
            : cfg =>
            {
                existing(cfg);
                configure(cfg);
            };

        return this;
    }

    /// <summary>
    /// Registers module consumers, sagas, and outbox on the RabbitMQ bus.
    /// Call this from the host application (not from the library) to keep
    /// the library free of compile-time module assembly references.
    /// </summary>
    /// <param name="configure">
    /// Delegate that adds consumers/sagas to the bus configurator.
    /// Multiple calls are composed — later calls append to earlier ones.
    /// </param>
    public SaasBuilderTransportOptions WithBusConsumers(
        Action<IBusRegistrationConfigurator> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        Action<IBusRegistrationConfigurator>? existing = BusConsumers;
        BusConsumers = existing is null
            ? configure
            : cfg =>
            {
                existing(cfg);
                configure(cfg);
            };

        return this;
    }
}
