using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Chassis.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Chassis.Persistence;

/// <summary>
/// Abstract EF Core base context that automatically applies tenant global query filters
/// to all entity types implementing <see cref="ITenantScoped"/>.
/// </summary>
/// <remarks>
/// <para>
/// Derived contexts (one per bounded context / module) call <c>base.OnModelCreating(builder)</c>
/// and then perform their own entity configuration. The filter is applied once at model-build
/// time via reflection over the registered entity types — this incurs no per-query overhead.
/// </para>
/// <para>
/// The filter expression evaluates <see cref="ITenantContextAccessor.Current"/> at query
/// execution time (not model-build time). When the accessor's bypass scope is active
/// (<see cref="ITenantContextAccessor.BeginBypass"/>) or when <c>Current</c> is <see langword="null"/>
/// (e.g. OpenIddict internal queries, dev seeding), the filter is a no-op — all rows are
/// returned. RLS policies on the database remain the defence in depth for tenant-scoped tables.
/// </para>
/// </remarks>
public abstract class ChassisDbContext : DbContext
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    /// <summary>
    /// Initializes the base context with a tenant context accessor.
    /// </summary>
    /// <param name="options">The EF Core context options.</param>
    /// <param name="tenantContextAccessor">
    /// The ambient tenant context accessor. Must be registered as a singleton in DI.
    /// </param>
    protected ChassisDbContext(
        DbContextOptions options,
        ITenantContextAccessor tenantContextAccessor)
        : base(options)
    {
        _tenantContextAccessor = tenantContextAccessor
            ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Reflects over all registered entity types and applies a global query filter
    /// for each type that implements <see cref="ITenantScoped"/>.
    /// </summary>
    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            MethodInfo? methodNullable = typeof(ChassisDbContext)
                .GetMethod(nameof(BuildTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance);

            // GetMethod returns null if not found; this is a programming error if it happens.
            MethodInfo method = methodNullable
                ?? throw new InvalidOperationException("BuildTenantFilter method not found via reflection.");

            // BuildTenantFilter is generic; create the closed generic for this entity type.
            MethodInfo genericMethod = method.MakeGenericMethod(entityType.ClrType);

            // The method returns a LambdaExpression which we pass to HasQueryFilter.
            LambdaExpression filterLambda = (LambdaExpression)(genericMethod.Invoke(this, null)
                ?? throw new InvalidOperationException("BuildTenantFilter returned null."));
            entityType.SetQueryFilter(filterLambda);
        }
    }

    /// <summary>
    /// Builds the tenant filter expression for <typeparamref name="TEntity"/>.
    /// Captures <c>this</c> (not the tenant id directly) so the expression evaluates
    /// <see cref="ITenantContextAccessor.Current"/> at query time, not at model-build time.
    /// </summary>
    private Expression<Func<TEntity, bool>> BuildTenantFilter<TEntity>()
        where TEntity : class, ITenantScoped
    {
        // 'this' is captured — the lambda re-evaluates Current on every query execution.
        return entity => IsTenantFilterBypassed() || entity.TenantId == GetCurrentTenantId();
    }

    /// <summary>
    /// Returns <see langword="true"/> when the bypass scope is active (e.g. OpenIddict
    /// internal queries, dev seeding) or when no tenant context exists. In either case
    /// the global query filter is a no-op and RLS on the DB side is the remaining defence.
    /// </summary>
    private bool IsTenantFilterBypassed()
        => _tenantContextAccessor.IsBypassed || _tenantContextAccessor.Current is null;

    /// <summary>
    /// Returns the current tenant id.
    /// Called at query-execution time by the global query filter expression only when
    /// <see cref="IsTenantFilterBypassed"/> is <see langword="false"/>.
    /// </summary>
    private Guid GetCurrentTenantId()
    {
        // Current is guaranteed non-null when this method is reached because
        // IsTenantFilterBypassed() returns true (short-circuiting the &&) when Current is null.
        return _tenantContextAccessor.Current!.TenantId;
    }
}
