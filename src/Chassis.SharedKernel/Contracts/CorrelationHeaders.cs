namespace Chassis.SharedKernel.Contracts;

/// <summary>
/// Well-known HTTP header and MassTransit message-header key names used for
/// distributed tracing and tenant propagation across the chassis.
/// Use these constants wherever headers are read or written to avoid magic strings.
/// </summary>
public static class CorrelationHeaders
{
    /// <summary>Header carrying the tenant identifier (UUID string).</summary>
    public const string TenantId = "tenant-id";

    /// <summary>Header carrying the authenticated user identifier (UUID string).</summary>
    public const string UserId = "user-id";

    /// <summary>
    /// Header carrying the correlation identifier for end-to-end request tracing.
    /// Propagated from inbound HTTP requests through MassTransit message headers.
    /// </summary>
    public const string CorrelationId = "correlation-id";

    /// <summary>
    /// W3C Trace Context <c>traceparent</c> header.
    /// Propagated end-to-end so traces span HTTP boundaries and bus consumers.
    /// See https://www.w3.org/TR/trace-context/.
    /// </summary>
    public const string TraceParent = "traceparent";
}
