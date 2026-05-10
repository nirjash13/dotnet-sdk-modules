using Identity.Domain.Authorization;
using Identity.Domain.Entities;
using Identity.Domain.Organizations;
using Identity.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Identity bounded context.
/// Inherits <see cref="SaasBuilderDbContext"/> for automatic tenant query filters and
/// the <c>TenantCommandInterceptor</c> that issues <c>SET LOCAL app.tenant_id</c>
/// before every command.
/// </summary>
/// <remarks>
/// <para>
/// <c>UseOpenIddict()</c> is called inside <see cref="OnModelCreating"/> to register
/// the four OpenIddict entity sets (Applications, Authorizations, Scopes, Tokens)
/// against this context's model.
/// </para>
/// <para>
/// OpenIddict's built-in entity types do not implement <see cref="ITenantScoped"/>,
/// so the inherited global query filter is NOT applied to them. Tenant isolation for
/// OpenIddict tables is enforced via explicit RLS policies in migration 002_TenantIdColumns.
/// </para>
/// </remarks>
public sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the user aggregate root set.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Gets the tenant membership set.</summary>
    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();

    // ── Phase 2 — Organizations & RBAC ────────────────────────────────────────

    /// <summary>Gets the organization set.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>Gets the organization member set.</summary>
    public DbSet<Member> OrganizationMembers => Set<Member>();

    /// <summary>Gets the invitation set.</summary>
    public DbSet<Invitation> Invitations => Set<Invitation>();

    /// <summary>Gets the role set.</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>Gets the permission set.</summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>Gets the role-permission join set.</summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Must be called before UseOpenIddict() so base class tenant filters are registered first.
        base.OnModelCreating(modelBuilder);

        // Register OpenIddict entity type configurations against this context.
        modelBuilder.UseOpenIddict();

        ConfigureUser(modelBuilder);
        ConfigureUserTenantMembership(modelBuilder);

        // Phase 2 — apply entity type configurations via IEntityTypeConfiguration<T> classes.
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new MemberConfiguration());
        modelBuilder.ApplyConfiguration(new InvitationConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", "identity");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(u => u.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.UpdatedAt);

            entity.HasIndex(u => u.Email).IsUnique();

            // Navigation — memberships owned by User aggregate.
            entity.HasMany(u => u.Memberships)
                .WithOne()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUserTenantMembership(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserTenantMembership>(entity =>
        {
            entity.ToTable("user_tenant_memberships", "identity");
            entity.HasKey(m => m.Id);

            entity.Property(m => m.TenantId).IsRequired();
            entity.Property(m => m.UserId).IsRequired();
            entity.Property(m => m.IsPrimary).IsRequired();

            entity.Property(m => m.Roles)
                .HasColumnType("text[]");

            entity.Property(m => m.CreatedAt).IsRequired();

            // A user may have at most one primary membership.
            entity.HasIndex(m => new { m.UserId, m.IsPrimary })
                .HasFilter("\"IsPrimary\" = true")
                .IsUnique();
        });
    }
}
