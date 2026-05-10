using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Application.Models;

namespace Audit.Infrastructure.Loggers;

// ---------------------------------------------------------------------------
// SIEM / forwarder stubs — Phase 5.4
// ---------------------------------------------------------------------------

/// <summary>TODO(Phase 5.4): Splunk HEC forwarder — install Splunk.Logging NuGet package.</summary>
internal sealed class SplunkHecAuditForwarder : IAuditLogger
{
    public Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.4): Splunk HEC integration");
}

/// <summary>TODO(Phase 5.4): Datadog audit forwarder — install Datadog NuGet package.</summary>
internal sealed class DatadogAuditForwarder : IAuditLogger
{
    public Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.4): Datadog integration");
}

/// <summary>TODO(Phase 5.4): Syslog audit forwarder — install a syslog NuGet package.</summary>
internal sealed class SyslogAuditForwarder : IAuditLogger
{
    public Task RecordAsync(AuditEvent evt, CancellationToken ct = default)
        => throw new System.NotImplementedException("TODO(Phase 5.4): Syslog integration");
}
