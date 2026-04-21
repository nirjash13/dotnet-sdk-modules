using Chassis.Persistence;
using Chassis.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Registration.Application.Sagas;

namespace Registration.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Registration bounded context.
/// Inherits <see cref="ChassisDbContext"/> for the standard chassis interceptor chain.
/// </summary>
/// <remarks>
/// <para>
/// Schema: <c>registration</c>.
/// </para>
/// <para>
/// The <see cref="RegistrationSagaState"/> entity does NOT implement <c>ITenantScoped</c>.
/// The tenant being provisioned does not yet exist when the saga starts, so the EF Core
/// global tenant query filter and Postgres RLS are deliberately not applied to this table.
/// See <c>migrations/registration/001_initial_registration.sql</c> for the SQL-level comment.
/// </para>
/// </remarks>
public sealed class RegistrationDbContext(
    DbContextOptions<RegistrationDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : ChassisDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the saga state set.</summary>
    public DbSet<RegistrationSagaState> RegistrationSagaStates => Set<RegistrationSagaState>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Base applies tenant query filters for ITenantScoped entities.
        // RegistrationSagaState does NOT implement ITenantScoped, so no filter is applied.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new RegistrationSagaStateConfiguration());
    }
}
