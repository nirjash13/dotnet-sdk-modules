using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Registration.Application.Sagas;

namespace Registration.Infrastructure.Persistence;

/// <summary>
/// EF Core entity type configuration for <see cref="RegistrationSagaState"/>.
/// Configures the table, primary key, column constraints, and indexes.
/// </summary>
internal sealed class RegistrationSagaStateConfiguration : IEntityTypeConfiguration<RegistrationSagaState>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RegistrationSagaState> entity)
    {
        entity.ToTable("registration_saga_state", "registration");

        entity.HasKey(s => s.CorrelationId);
        entity.Property(s => s.CorrelationId).HasColumnName("correlation_id");
        entity.Property(s => s.CurrentState).HasMaxLength(64).IsRequired().HasColumnName("current_state");
        entity.Property(s => s.TenantId).HasColumnName("tenant_id");
        entity.Property(s => s.AssociationName).HasMaxLength(256).IsRequired().HasColumnName("association_name");
        entity.Property(s => s.PrimaryUserEmail).HasMaxLength(256).IsRequired().HasColumnName("primary_user_email");
        entity.Property(s => s.Currency).HasMaxLength(3).IsRequired().HasColumnName("currency");
        entity.Property(s => s.UserId).HasColumnName("user_id");
        entity.Property(s => s.AccountId).HasColumnName("account_id");
        entity.Property(s => s.ReportingId).HasColumnName("reporting_id");
        entity.Property(s => s.StartedAt).HasColumnName("started_at");
        entity.Property(s => s.CompletedAt).HasColumnName("completed_at");
        entity.Property(s => s.FailureReason).HasMaxLength(2048).HasColumnName("failure_reason");

        // Index on current_state for health dashboards and saga monitoring queries.
        entity.HasIndex(s => s.CurrentState).HasDatabaseName("ix_registration_saga_state_current_state");
    }
}
