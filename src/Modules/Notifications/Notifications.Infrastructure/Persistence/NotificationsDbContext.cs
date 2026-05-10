using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;
using SaasBuilder.Persistence;
using SaasBuilder.SharedKernel.Tenancy;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// EF Core bounded-context for the Notifications module.
/// Inherits tenant global query filters from <see cref="SaasBuilderDbContext"/>.
/// </summary>
public sealed class NotificationsDbContext(
    DbContextOptions<NotificationsDbContext> options,
    ITenantContextAccessor tenantContextAccessor)
    : SaasBuilderDbContext(options, tenantContextAccessor)
{
    /// <summary>Gets the in-app notifications set.</summary>
    public DbSet<InAppNotification> InAppNotifications => Set<InAppNotification>();

    /// <summary>Gets the notification preferences set.</summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("notifications");

        modelBuilder.Entity<InAppNotification>(e =>
        {
            e.ToTable("in_app_notifications");
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).HasMaxLength(256).IsRequired();
            e.Property(n => n.Body).HasMaxLength(4096);
            e.Property(n => n.ActionUrl).HasMaxLength(2048);
            e.HasIndex(n => new { n.TenantId, n.UserId, n.CreatedAt });
        });

        modelBuilder.Entity<NotificationPreference>(e =>
        {
            e.ToTable("notification_preferences");
            e.HasKey(p => p.Id);
            e.Property(p => p.NotificationType).HasMaxLength(128).IsRequired();
            e.HasIndex(p => new { p.TenantId, p.UserId, p.Channel, p.NotificationType }).IsUnique();
        });
    }
}
