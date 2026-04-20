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
/// execution time (not model-build time), so the accessor must be non-null when a query runs.
/// If <see cref="ITenantContextAccessor.Current"/> is null at query execution, the filter
/// throws <see cref="InvalidOperationException"/> with a diagnostic message — fail-fast
/// rather than leaking cross-tenant data.
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

            MethodInfo method = typeof(ChassisDbContext)
                .GetMethod(nameof(BuildTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

            // BuildTenantFilter is generic; create the closed generic for this entity type.
            MethodInfo genericMethod = method.MakeGenericMethod(entityType.ClrType);

            // The method returns a LambdaExpression which we pass to HasQueryFilter.
            LambdaExpression filterLambda = (LambdaExpression)genericMethod.Invoke(this, null)!;
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
        return entity => entity.TenantId == GetCurrentTenantId();
    }

    /// <summary>
    /// Returns the current tenant id, throwing if the context is not established.
    /// Called at query-execution time by the global query filter expression.
    /// </summary>
    private Guid GetCurrentTenantId()
    {
        ITenantContext? ctx = _tenantContextAccessor.Current;
        if (ctx is null)
        {
            throw new InvalidOperationException(
                "No ambient tenant context is established. " +
                "Ensure TenantMiddleware has run before any EF Core query is executed, " +
                "or explicitly set ITenantContextAccessor.Current in background services.");
        }

        return ctx.TenantId;
    }
}
