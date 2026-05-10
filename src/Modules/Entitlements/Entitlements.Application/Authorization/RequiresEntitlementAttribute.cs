using System;
using Microsoft.AspNetCore.Authorization;

namespace Entitlements.Application.Authorization;

/// <summary>
/// Gates access to an endpoint or controller based on an entitlement key.
/// Combine with <see cref="RequiresEntitlementAuthorizationHandler"/> registered in DI.
/// </summary>
/// <example>
/// <code>
/// [RequiresEntitlement("advanced_reporting")]
/// public IResult GetAdvancedReport(...) { ... }
///
/// [RequiresEntitlement("max_seats", AsLimit = true)]
/// public IResult AddMember(...) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresEntitlementAttribute : Attribute, IAuthorizationRequirement
{
    /// <summary>Initializes the attribute with the required entitlement key.</summary>
    /// <param name="key">The entitlement key to check.</param>
    public RequiresEntitlementAttribute(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Entitlement key must not be empty.", nameof(key));
        }

        Key = key;
    }

    /// <summary>Gets the entitlement key this requirement checks.</summary>
    public string Key { get; }

    /// <summary>
    /// Gets or sets a value indicating whether to treat this entitlement as a numeric limit.
    /// When <see langword="true"/>, the handler counts current usage against the limit and
    /// returns 402 when the limit is exceeded.
    /// TODO(Phase 4 — usage counter integration): Implement the AsLimit=true path with
    /// a per-resource usage counter injected via a delegate or IUsageMeter.
    /// </summary>
    public bool AsLimit { get; init; }
}
