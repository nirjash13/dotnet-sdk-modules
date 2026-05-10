using System;

namespace Audit.Application.Interceptors;

/// <summary>
/// Marks an entity for automatic audit logging via <c>EfCoreAuditInterceptor</c>.
/// Only entity types decorated with this attribute will emit audit events on Add/Update/Delete.
/// The interceptor is disabled by default; enable it via <c>AuditOptions.EnableEfCoreInterceptor = true</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AuditableAttribute : Attribute
{
    /// <summary>Gets or sets the resource type name used in audit events (defaults to the class name).</summary>
    public string? ResourceTypeName { get; set; }
}
