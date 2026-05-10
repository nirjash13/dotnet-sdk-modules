using System;
using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Application.Models;
using Audit.Infrastructure.Entities;
using Audit.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Audit.Infrastructure.Loggers;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogger"/>.
/// Writes to the append-only <c>audit_entries</c> table.
/// Each entry is an INSERT — no UPDATE or DELETE operations are ever performed by this class.
/// </summary>
internal sealed class EfCoreAuditLogger(
    AuditDbContext db,
    ILogger<EfCoreAuditLogger> logger)
    : IAuditLogger
{
    /// <inheritdoc />
    public async Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        try
        {
            AuditEntry entry = new AuditEntry(
                id: Guid.NewGuid(),
                tenantId: evt.TenantId,
                actorId: evt.ActorId,
                action: evt.Action,
                resourceType: evt.ResourceType,
                resourceId: evt.ResourceId,
                beforeJson: evt.BeforeJson,
                afterJson: evt.AfterJson,
                ipAddress: evt.IpAddress,
                userAgent: evt.UserAgent,
                correlationId: evt.CorrelationId,
                timestamp: evt.Timestamp ?? DateTimeOffset.UtcNow);

            await db.AuditEntries.AddAsync(entry, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Append-only audit log must not break the calling operation.
            logger.LogError(
                ex,
                "Audit.EfCore: failed to record audit event (action={Action}, resource={ResourceType}/{ResourceId})",
                evt.Action, evt.ResourceType, evt.ResourceId);
        }
    }
}
