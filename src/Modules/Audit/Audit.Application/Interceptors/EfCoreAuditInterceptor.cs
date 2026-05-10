using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Audit.Application.Abstractions;
using Audit.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using SaasBuilder.SharedKernel.Tenancy;

namespace Audit.Application.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that automatically emits audit events
/// for entity types decorated with <see cref="AuditableAttribute"/>.
/// Default-off: register only when <c>AuditOptions.EnableEfCoreInterceptor == true</c>.
/// </summary>
public sealed class EfCoreAuditInterceptor(
    IAuditLogger auditLogger,
    ITenantContextAccessor tenantAccessor)
    : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return result;
        }

        ITenantContext? ctx = tenantAccessor.Current;
        if (ctx is null)
        {
            return result;
        }

        IEnumerable<EntityEntry> auditable = eventData.Context.ChangeTracker
            .Entries()
            .Where(e => e.Entity.GetType().GetCustomAttributes(typeof(AuditableAttribute), true).Length > 0
                     && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        foreach (EntityEntry entry in auditable)
        {
            AuditableAttribute attr = (AuditableAttribute)entry.Entity
                .GetType().GetCustomAttributes(typeof(AuditableAttribute), true)[0];

            string resourceType = attr.ResourceTypeName ?? entry.Entity.GetType().Name;
            string resourceId = GetPrimaryKeyString(entry);
            string action = entry.State switch
            {
                EntityState.Added => $"{resourceType}.created",
                EntityState.Modified => $"{resourceType}.updated",
                EntityState.Deleted => $"{resourceType}.deleted",
                _ => $"{resourceType}.unknown",
            };

            string? beforeJson = entry.State == EntityState.Added ? null
                : TrySerialize(entry.OriginalValues);
            string? afterJson = entry.State == EntityState.Deleted ? null
                : TrySerialize(entry.CurrentValues);

            AuditEvent evt = new AuditEvent(
                TenantId: ctx.TenantId,
                ActorId: ctx.UserId?.ToString() ?? "system",
                Action: action,
                ResourceType: resourceType,
                ResourceId: resourceId,
                BeforeJson: beforeJson,
                AfterJson: afterJson,
                CorrelationId: ctx.CorrelationId);

            await auditLogger.RecordAsync(evt, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private static string GetPrimaryKeyString(EntityEntry entry)
    {
        object? pk = entry.Properties
            .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue;
        return pk?.ToString() ?? "unknown";
    }

    private static string? TrySerialize(PropertyValues values)
    {
        try
        {
            Dictionary<string, object?> dict = new Dictionary<string, object?>();
            foreach (IProperty prop in values.Properties)
            {
                dict[prop.Name] = values[prop];
            }

            return JsonSerializer.Serialize(dict);
        }
        catch
        {
            return null;
        }
    }
}
