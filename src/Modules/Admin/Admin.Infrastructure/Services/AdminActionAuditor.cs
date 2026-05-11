using System;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Infrastructure.Persistence;
using Admin.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace Admin.Infrastructure.Services;

/// <summary>
/// Persists admin action audit records to the <c>admin_action_audit</c> table.
/// Never throws — audit failures are non-fatal per the interface contract.
/// </summary>
public sealed class AdminActionAuditor(
    AdminDbContext dbContext,
    ILogger<AdminActionAuditor> logger) : IAdminActionAuditor
{
    /// <inheritdoc />
    public async Task RecordAsync(
        string actorId,
        string action,
        Guid? targetTenantId,
        string? payloadJson,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        try
        {
            AdminActionAuditEntry entry = AdminActionAuditEntry.Create(
                actorId,
                action,
                targetTenantId,
                payloadJson,
                ipAddress,
                userAgent);

            dbContext.AdminActionAuditEntries.Add(entry);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Audit failures must not propagate to callers.
            logger.LogError(
                ex,
                "Failed to persist admin action audit record: actor={ActorId}, action={Action}.",
                actorId,
                action);
        }
    }
}
