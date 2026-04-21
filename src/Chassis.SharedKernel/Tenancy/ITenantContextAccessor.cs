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

    /// <summary>
    /// Gets a value indicating whether tenant enforcement is currently bypassed for this
    /// execution scope. When <see langword="true"/>, EF Core global query filters and the
    /// <c>TenantCommandInterceptor</c> skip tenant enforcement entirely, allowing queries
    /// against non-tenant-scoped tables (e.g. OpenIddict tables, dev seeding).
    /// </summary>
    bool IsBypassed { get; }

    /// <summary>
    /// Opens a bypass scope that suspends tenant enforcement for the current async execution
    /// context. Dispose the returned handle to restore enforcement.
    /// </summary>
    /// <remarks>
    /// Intended for infrastructure code that must query the database before a tenant context
    /// exists: OpenIddict token issuance, dev client seeding, and claim enrichment during
    /// initial sign-in. Business-layer code must NEVER call this method.
    /// </remarks>
    IDisposable BeginBypass();
}
