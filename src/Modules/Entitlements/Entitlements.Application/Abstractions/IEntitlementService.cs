using System.Threading;
using System.Threading.Tasks;

namespace Entitlements.Application.Abstractions;

/// <summary>
/// Service that evaluates entitlements for the current tenant.
/// Entitlements are derived from the tenant's active edition (from the Billing module)
/// plus any tenant-level override grants (sales exceptions).
/// Results are cached per (tenantId, key) and invalidated on <c>SubscriptionUpdatedIntegrationEvent</c>.
/// </summary>
public interface IEntitlementService
{
    /// <summary>
    /// Returns <see langword="true"/> when the current tenant holds the specified boolean entitlement.
    /// </summary>
    Task<bool> HasAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns the numeric limit for the specified entitlement, or <see langword="null"/>
    /// if the entitlement is not defined or is not a numeric type.
    /// </summary>
    Task<long?> GetLimitAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns the string value for the specified entitlement, or <see langword="null"/>
    /// if the entitlement is not defined or is not a string type.
    /// </summary>
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);
}
