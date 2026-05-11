using System;
using SaasBuilder.SharedKernel.Tenancy;

namespace Ai.Domain.Entities;

/// <summary>
/// Append-only record of a single LLM request's token consumption and cost.
/// Scoped to a tenant; never updated after creation.
/// </summary>
public sealed class LlmUsageRecord : ITenantScoped
{
    /// <summary>Gets the unique record identifier.</summary>
    public Guid Id { get; private set; }

    /// <inheritdoc/>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the user who triggered the request.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Gets the model identifier used (e.g. "gpt-4o").</summary>
    public string Model { get; private set; } = string.Empty;

    /// <summary>Gets the number of prompt (input) tokens consumed.</summary>
    public int PromptTokens { get; private set; }

    /// <summary>Gets the number of completion (output) tokens generated.</summary>
    public int CompletionTokens { get; private set; }

    /// <summary>Gets the estimated cost in USD based on provider pricing tables.</summary>
    public decimal CostUsd { get; private set; }

    /// <summary>Gets the correlation request identifier for tracing.</summary>
    public string RequestId { get; private set; } = string.Empty;

    /// <summary>Gets the UTC timestamp when the request was made.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core parameterless constructor.
    private LlmUsageRecord() { }

    /// <summary>Creates a new usage record.</summary>
    public static LlmUsageRecord Create(
        Guid tenantId,
        Guid userId,
        string model,
        int promptTokens,
        int completionTokens,
        decimal costUsd,
        string requestId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model must not be empty.", nameof(model));
        }

        return new LlmUsageRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            CostUsd = costUsd,
            RequestId = requestId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
