using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Application.Evaluation;
using Ai.Contracts;
using Ai.Domain.Entities;
using Ai.Infrastructure.Extensions;
using Ai.Infrastructure.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Abstractions;
using SaasBuilder.SharedKernel.Tenancy;

namespace Ai.Api;

/// <summary>
/// <see cref="IModuleStartup"/> implementation for the AI module.
/// Registers all AI services and maps endpoints under <c>/api/v1/ai</c>.
/// All endpoints require authorisation; streaming uses SSE.
/// </summary>
public sealed class AiModule : IModuleStartup
{
    /// <inheritdoc/>
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddAiInfrastructure(config);
    }

#if NET10_0_OR_GREATER
    /// <inheritdoc/>
    public void Configure(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder ai = endpoints
            .MapGroup("/api/v1/ai")
            .WithTags("ai")
            .RequireAuthorization();

        // POST /api/v1/ai/chat
        ai.MapPost("/chat", ChatAsync)
            .WithName("Ai_Chat")
            .WithSummary("Non-streaming chat completion. Records usage and checks budget.");

        // POST /api/v1/ai/chat/stream
        ai.MapPost("/chat/stream", ChatStreamAsync)
            .WithName("Ai_ChatStream")
            .WithSummary("Server-Sent Events streaming chat completion.");

        // POST /api/v1/ai/embed
        ai.MapPost("/embed", EmbedAsync)
            .WithName("Ai_Embed")
            .WithSummary("Generates a vector embedding for the supplied text.");

        // POST /api/v1/ai/rag/query
        ai.MapPost("/rag/query", RagQueryAsync)
            .WithName("Ai_RagQuery")
            .WithSummary("RAG pipeline: embed → tenant-scoped vector search → LLM answer.");

        // POST /api/v1/ai/vectors
        ai.MapPost("/vectors", UpsertVectorAsync)
            .WithName("Ai_UpsertVector")
            .WithSummary("Upserts a document into the tenant's vector store.");

        // DELETE /api/v1/ai/vectors/{id}
        ai.MapDelete("/vectors/{id:guid}", DeleteVectorAsync)
            .WithName("Ai_DeleteVector")
            .WithSummary("Removes a document from the tenant's vector store.");

        // POST /api/v1/ai/eval
        ai.MapPost("/eval", RunEvalAsync)
            .WithName("Ai_RunEval")
            .WithSummary("Runs the golden-set evaluation suite against the configured LLM pipeline.");

        // POST /api/v1/ai/mcp  — MCP JSON-RPC 2.0 endpoint
        ai.MapPost("/mcp", McpAsync)
            .WithName("Ai_Mcp")
            .WithSummary("Model Context Protocol JSON-RPC 2.0 endpoint (stub).");
    }

    private static async Task<IResult> ChatAsync(
        ChatRequest request,
        ILlmClient llm,
        ILlmBudgetTracker budget,
        ITenantContextAccessor tenantAccessor,
        ILogger<AiModule> logger,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        if (await budget.IsBudgetExceededAsync(tenant.TenantId, ct).ConfigureAwait(false))
        {
            return Results.Problem(
                detail: "Monthly AI budget has been exhausted. Upgrade your plan or wait for the next billing period.",
                statusCode: StatusCodes.Status402PaymentRequired,
                title: "AI budget exceeded");
        }

        ChatResponse response = await llm.ChatAsync(request, ct).ConfigureAwait(false);

        // Record usage asynchronously (fire-and-forget inside a try/catch — budget tracking must not
        // block the user response on failure).
        _ = RecordUsageSafeAsync(budget, tenant.TenantId, response, logger);

        decimal fraction = await budget.GetBudgetFractionAsync(tenant.TenantId, ct).ConfigureAwait(false);
        var httpContext = GetHttpContext(tenantAccessor);
        if (fraction >= 0.8m && httpContext is not null)
        {
            httpContext.Response.Headers["X-Ai-Budget-Warning"] =
                $"Budget {fraction:P0} consumed this month.";
        }

        return Results.Ok(response);
    }

    private static async Task<IResult> ChatStreamAsync(
        ChatRequest request,
        ILlmClient llm,
        ITenantContextAccessor tenantAccessor,
        HttpResponse httpResponse,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        httpResponse.ContentType = "text/event-stream";
        httpResponse.Headers.CacheControl = "no-cache";

        await foreach (string token in llm.StreamChatAsync(request, ct).ConfigureAwait(false))
        {
            string sseData = $"data: {JsonSerializer.Serialize(token)}\n\n";
            await httpResponse.WriteAsync(sseData, ct).ConfigureAwait(false);
            await httpResponse.Body.FlushAsync(ct).ConfigureAwait(false);
        }

        await httpResponse.WriteAsync("data: [DONE]\n\n", ct).ConfigureAwait(false);
        return Results.Empty;
    }

    private static async Task<IResult> EmbedAsync(
        EmbeddingRequest request,
        IEmbeddingClient embeddingClient,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        if (tenantAccessor.Current is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { detail = "text is required." });
        }

        float[] embedding = await embeddingClient.EmbedAsync(request.Text, ct).ConfigureAwait(false);

        return Results.Ok(new EmbeddingResponse { Embedding = embedding, Model = request.Model ?? "default" });
    }

    private static async Task<IResult> RagQueryAsync(
        RagQueryRequest request,
        IRagPipeline rag,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.BadRequest(new { detail = "question is required." });
        }

        ChatResponse response = await rag.QueryAsync(request.Question, tenant.TenantId, ct).ConfigureAwait(false);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpsertVectorAsync(
        UpsertVectorRequest request,
        IVectorStore vectorStore,
        IEmbeddingClient embeddingClient,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest(new { detail = "content is required." });
        }

        float[] embedding = await embeddingClient.EmbedAsync(request.Content, ct).ConfigureAwait(false);
        string embeddingJson = "[" + string.Join(",", embedding) + "]";

        VectorDocument doc = VectorDocument.Create(
            tenant.TenantId,
            request.Content,
            embeddingJson,
            request.MetadataJson ?? "{}");

        await vectorStore.UpsertAsync(doc, ct).ConfigureAwait(false);

        return Results.Created($"/api/v1/ai/vectors/{doc.Id}", new { id = doc.Id });
    }

    private static async Task<IResult> DeleteVectorAsync(
        Guid id,
        IVectorStore vectorStore,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        ITenantContext? tenant = tenantAccessor.Current;
        if (tenant is null)
        {
            return Results.Unauthorized();
        }

        await vectorStore.DeleteAsync(id, tenant.TenantId, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> RunEvalAsync(
        IPromptEvaluator evaluator,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        if (tenantAccessor.Current is null)
        {
            return Results.Unauthorized();
        }

        IReadOnlyList<EvalResult> results = await evaluator.RunAsync(ct).ConfigureAwait(false);
        return Results.Ok(results);
    }

    private static async Task<IResult> McpAsync(
        HttpRequest httpRequest,
        McpServerEndpoint mcp,
        ITenantContextAccessor tenantAccessor,
        CancellationToken ct)
    {
        if (tenantAccessor.Current is null)
        {
            return Results.Unauthorized();
        }

        JsonElement body = await httpRequest.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        object result = await mcp.HandleAsync(body, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task RecordUsageSafeAsync(
        ILlmBudgetTracker budget,
        Guid tenantId,
        ChatResponse response,
        ILogger logger)
    {
        try
        {
            var record = LlmUsageRecord.Create(
                tenantId,
                Guid.Empty, // userId resolved from HttpContext in full impl
                "unknown",  // model resolved from response in full impl
                response.Usage.PromptTokens,
                response.Usage.CompletionTokens,
                response.Usage.CostUsd,
                Guid.NewGuid().ToString());

            await budget.RecordUsageAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record LLM usage — budget tracking will be inaccurate.");
        }
    }

    // Narrow helper to get HttpContext without injecting it directly into the handler.
    private static HttpContext? GetHttpContext(ITenantContextAccessor tenantAccessor)
    {
        // tenantAccessor may be backed by HttpContext in ASP.NET Core — no direct dependency.
        // For now return null; header injection is best-effort.
        return null;
    }
#endif

    /// <summary>Request body for RAG query.</summary>
    public sealed class RagQueryRequest
    {
        /// <summary>Gets or sets the natural-language question.</summary>
        public string? Question { get; set; }
    }

    /// <summary>Request body for vector upsert.</summary>
    public sealed class UpsertVectorRequest
    {
        /// <summary>Gets or sets the plain-text content to index.</summary>
        public string? Content { get; set; }

        /// <summary>Gets or sets the optional JSON metadata string.</summary>
        public string? MetadataJson { get; set; }
    }
}
