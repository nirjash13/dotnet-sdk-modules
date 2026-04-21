using System;
using System.Threading.Tasks;
using MassTransit;
using Registration.Contracts;

namespace Reporting.Application.Consumers;

/// <summary>
/// Handles the <see cref="ProvisionReporting"/> command sent by the registration saga.
/// Bootstraps any tenant-specific reporting configuration and responds with <see cref="ReportingProvisioned"/>.
/// </summary>
/// <remarks>
/// Phase 5 stub: no persistent state is written because Reporting uses a read-model projection.
/// The ReportingId returned is a deterministic Guid derived from the TenantId so that retries
/// return the same value (idempotent). A real implementation would write a tenant config row.
/// </remarks>
public sealed class ProvisionReportingCommandConsumer : IConsumer<ProvisionReporting>
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<ProvisionReporting> context)
    {
        ProvisionReporting command = context.Message;

        // Deterministic ReportingId: derived from TenantId so retries return the same value.
        // XOR with a fixed domain namespace Guid to avoid collision with raw tenant Guid usage.
        Guid reportingId = DeriveReportingId(command.TenantId);

        return context.Publish(new ReportingProvisioned
        {
            CorrelationId = command.CorrelationId,
            ReportingId = reportingId,
        });
    }

    /// <summary>
    /// Derives a deterministic reporting bootstrap id from the tenant id.
    /// Stable across retries; safe to return from compensation rollback as well.
    /// </summary>
    private static Guid DeriveReportingId(Guid tenantId)
    {
        // Simple XOR with a fixed namespace; sufficient for a stub.
        // Replace with a DB-backed lookup in Phase 6+.
        byte[] bytes = tenantId.ToByteArray();
        bytes[0] ^= 0xAB;
        bytes[15] ^= 0xCD;
        return new Guid(bytes);
    }
}
