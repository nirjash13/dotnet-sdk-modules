using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Admin.Application.Abstractions;
using Admin.Contracts;
using Admin.Infrastructure.Persistence;
using Audit.Infrastructure.Persistence;
using Billing.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Admin.Infrastructure.Services;

/// <summary>
/// Cross-module tenant directory service.
/// Queries Identity (Organization), Billing (Subscription), and Audit (recent events).
/// All queries are read-only (AsNoTracking) — no side effects.
/// </summary>
public sealed class TenantDirectoryService(
    IdentityDbContext identityDbContext,
    BillingDbContext billingDbContext,
    AuditDbContext auditDbContext,
    AdminDbContext adminDbContext) : ITenantDirectoryService
{
    /// <inheritdoc />
    public async Task<(int Total, IReadOnlyList<TenantSummaryDto> Items)> ListTenantsAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IQueryable<Identity.Domain.Organizations.Organization> query =
            identityDbContext.Organizations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string lower = search.ToLowerInvariant();
            query = query.Where(o => o.Slug.Contains(lower) || o.Name.ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<Identity.Domain.Organizations.OrganizationStatus>(
                    status, ignoreCase: true, out var statusEnum))
            {
                query = query.Where(o => o.Status == statusEnum);
            }
        }

        int total = await query.CountAsync(ct).ConfigureAwait(false);

        var orgs = await query
            .OrderBy(o => o.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new { o.Id, o.TenantId, o.Slug, o.Name, o.Status, o.CreatedAt })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<TenantSummaryDto> items = orgs.Select(o => new TenantSummaryDto
        {
            Id = o.TenantId,
            Slug = o.Slug,
            Name = o.Name,
            Status = o.Status.ToString(),
            CreatedAt = o.CreatedAt,
        }).ToList();

        return (total, items);
    }

    /// <inheritdoc />
    public async Task<TenantInspectorDto?> GetTenantInspectorAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        Identity.Domain.Organizations.Organization? org = await identityDbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        if (org is null)
        {
            return null;
        }

        int memberCount = await identityDbContext.OrganizationMembers
            .AsNoTracking()
            .CountAsync(m => m.OrganizationId == org.Id, ct)
            .ConfigureAwait(false);

        Billing.Domain.Entities.Subscription? subscription = await billingDbContext.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct)
            .ConfigureAwait(false);

        var recentAuditRows = await auditDbContext.AuditEntries
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Take(5)
            .Select(a => new { a.Action, a.ActorId, a.Timestamp })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var lastAdminRow = await adminDbContext.AdminActionAuditEntries
            .AsNoTracking()
            .Where(a => a.TargetTenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new { a.ActorId, a.Timestamp })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        int totalAdminActions = await adminDbContext.AdminActionAuditEntries
            .AsNoTracking()
            .CountAsync(a => a.TargetTenantId == tenantId, ct)
            .ConfigureAwait(false);

        return new TenantInspectorDto
        {
            Id = tenantId,
            Slug = org.Slug,
            Name = org.Name,
            Status = org.Status.ToString(),
            CreatedAt = org.CreatedAt,
            MemberCount = memberCount,
            Subscription = subscription is null
                ? null
                : new TenantInspectorDto.SubscriptionSummary
                {
                    Id = subscription.Id,
                    PlanId = subscription.PlanId,
                    Status = subscription.Status.ToString(),
                    ProviderSubscriptionId = subscription.ProviderSubscriptionId,
                    StartedAt = subscription.StartedAt,
                    TrialEndsAt = subscription.TrialEndsAt,
                },
            RecentAuditEvents = recentAuditRows
                .Select(r => new TenantInspectorDto.RecentAuditEntry
                {
                    Action = r.Action,
                    ActorId = r.ActorId,
                    Timestamp = r.Timestamp,
                })
                .ToList(),
            Support = new TenantInspectorDto.SupportMetadata
            {
                LastAdminActionAt = lastAdminRow?.Timestamp,
                LastAdminActorId = lastAdminRow?.ActorId,
                TotalAdminActions = totalAdminActions,
            },
        };
    }
}
