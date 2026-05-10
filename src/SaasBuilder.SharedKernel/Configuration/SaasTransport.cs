namespace SaasBuilder.SharedKernel.Configuration;

/// <summary>
/// Selects the MassTransit transport used by the SaasBuilder host.
/// </summary>
public enum SaasTransport
{
    /// <summary>
    /// In-process MassTransit Mediator — no broker required.
    /// Suitable for local development and single-process deployments.
    /// </summary>
    InProc = 0,

    /// <summary>
    /// Out-of-process MassTransit Bus (RabbitMQ) with EF Core Outbox/Inbox.
    /// Requires RabbitMq:Host/Username/Password in configuration.
    /// </summary>
    Bus = 1,
}
