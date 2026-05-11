using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Domain.Entities;
using Ai.Infrastructure.VectorStores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ai.Infrastructure.Budgets;

/// <summary>
/// EF Core-backed implementation of <see cref="ILlmBudgetTracker"/>.
/// Reads the tenant's <c>ai_budget_usd_per_month</c> entitlement via
/// <see cref="IEntitlementValueProvider"/> and compares it to the sum of
/// <see cref="LlmUsageRecord.CostUsd"/> for the current calendar month.
/// <para>
/// Hard cap (100%): callers must return HTTP 402 Payment Required.
/// Soft cap (80%): callers should emit <c>X-Ai-Budget-Warning</c> response header.
/// </para>
/// </summary>
public sealed class EfCoreLlmBudgetTracker : ILlmBudgetTracker
{
    private readonly AiDbContext _db;
    private readonly IEntitlementValueProvider _entitlements;
    private readonly ILogger<EfCoreLlmBudgetTracker> _logger;

    /// <summary>Initializes a new instance of <see cref="EfCoreLlmBudgetTracker"/>.</summary>
    public EfCoreLlmBudgetTracker(
        AiDbContext db,
        IEntitlementValueProvider entitlements,
        ILogger<EfCoreLlmBudgetTracker> logger)
    {
        _db = db;
        _entitlements = entitlements;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RecordUsageAsync(LlmUsageRecord record, CancellationToken ct = default)
    {
        _db.LlmUsageRecords.Add(record);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogDebug(
            "LLM usage recorded: tenantId={TenantId}, model={Model}, cost={Cost:F6} USD",
            record.TenantId,
            record.Model,
            record.CostUsd);
    }

    /// <inheritdoc/>
    public async Task<bool> IsBudgetExceededAsync(Guid tenantId, CancellationToken ct = default)
    {
        decimal fraction = await GetBudgetFractionAsync(tenantId, ct).ConfigureAwait(false);
        return fraction >= 1.0m;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetBudgetFractionAsync(Guid tenantId, CancellationToken ct = default)
    {
        decimal budget = await _entitlements
            .GetValueAsync<decimal>(tenantId, "ai_budget_usd_per_month", ct)
            .ConfigureAwait(false);

        if (budget <= 0m)
        {
            // No budget configured — treat as unlimited (fraction = 0).
            return 0m;
        }

        DateTimeOffset periodStart = new DateTimeOffset(
            DateTimeOffset.UtcNow.Year,
            DateTimeOffset.UtcNow.Month,
            1,
            0, 0, 0,
            TimeSpan.Zero);

        decimal spent = await _db.LlmUsageRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.CreatedAt >= periodStart)
            .SumAsync(r => r.CostUsd, ct)
            .ConfigureAwait(false);

        return budget == 0m ? 0m : spent / budget;
    }
}

/// <summary>
/// Abstraction for reading typed entitlement values for a tenant.
/// Implemented in the Entitlements module — declared here as a narrow interface
/// to avoid a circular project reference between Ai.Infrastructure → Entitlements.
/// </summary>
public interface IEntitlementValueProvider
{
    /// <summary>Gets a typed entitlement value for the given tenant and key.</summary>
    Task<T> GetValueAsync<T>(Guid tenantId, string key, CancellationToken ct = default)
        where T : struct;
}
