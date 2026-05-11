using System;

namespace Gdpr.Contracts;

/// <summary>Published when the sub-processor list is modified (add/update/remove).</summary>
public sealed record SubProcessorListChangedEvent(
    Guid TenantId,
    string ChangeType,
    Guid SubProcessorId,
    string SubProcessorName);
