using System;
using System.Threading;
using System.Threading.Tasks;
using Ai.Domain.Entities;

namespace Ai.Application.Abstractions;

/// <summary>
/// Tracks per-tenant LLM spending against the edition's <c>ai_budget_usd_per_month</c>
/// entitlement.
/// </summary>
public interface ILlmBudgetTracker
{
    /// <summary>
    /// Persists a new usage record and updates the running spend total for the tenant's
    /// current billing period.
    /// </summary>
    Task RecordUsageAsync(LlmUsageRecord record, CancellationToken ct = default);

    /// <summary>
    /// Returns <see langword="true"/> when the tenant has consumed 100% or more of their
    /// monthly AI budget (hard cap — callers must return HTTP 402).
    /// </summary>
    Task<bool> IsBudgetExceededAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns a value in the range [0, ∞) representing the fraction of the budget
    /// consumed this period.  Values ≥ 0.8 trigger a soft-cap warning header
    /// (<c>X-Ai-Budget-Warning</c>).
    /// </summary>
    Task<decimal> GetBudgetFractionAsync(Guid tenantId, CancellationToken ct = default);
}
