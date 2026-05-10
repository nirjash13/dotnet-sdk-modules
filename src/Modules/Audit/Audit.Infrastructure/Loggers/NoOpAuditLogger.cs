using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Application.Models;
using Microsoft.Extensions.Logging;

namespace Audit.Infrastructure.Loggers;

/// <summary>
/// No-op audit logger registered when no database connection is configured.
/// Logs a WARNING per call so operators can detect misconfiguration.
/// </summary>
internal sealed class NoOpAuditLogger(ILogger<NoOpAuditLogger> logger) : IAuditLogger
{
    /// <inheritdoc />
    public Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
    {
        logger.LogWarning(
            "Audit.NoOp: event NOT recorded (action={Action}, resource={ResourceType}/{ResourceId}). " +
            "Configure a database connection string to enable audit logging.",
            evt.Action, evt.ResourceType, evt.ResourceId);
        return Task.CompletedTask;
    }
}
