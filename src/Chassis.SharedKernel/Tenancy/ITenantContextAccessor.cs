namespace Chassis.SharedKernel.Tenancy;

/// <summary>
/// Ambient-context accessor for <see cref="ITenantContext"/>.
/// The implementation in <c>Chassis.Host</c> is backed by <c>AsyncLocal&lt;ITenantContext?&gt;</c>
/// so context flows naturally across <c>await</c> continuations without leaking across
/// unrelated request scopes.
/// </summary>
public interface ITenantContextAccessor
{
    /// <summary>
    /// Gets or sets the current tenant context for the ambient execution scope.
    /// <see langword="null"/> when no tenant context has been established (e.g., before
    /// <c>TenantMiddleware</c> has run, or in background services that must set their own context).
    /// </summary>
    ITenantContext? Current { get; set; }
}
