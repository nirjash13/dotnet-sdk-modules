using System;

namespace Chassis.SharedKernel.Tenancy;

/// <summary>
/// Marker interface for domain entities that are scoped to a single tenant.
/// Any EF Core entity type that implements this interface automatically receives
/// a global query filter in <c>ChassisDbContext</c> that restricts rows to the
/// current <see cref="ITenantContextAccessor.Current"/> tenant.
/// Additionally, Postgres Row-Level Security policies keyed on <c>app.tenant_id</c>
/// provide a second line of defense even when the EF filter is bypassed.
/// </summary>
public interface ITenantScoped
{
    /// <summary>Gets the tenant this entity belongs to.</summary>
    Guid TenantId { get; }
}
