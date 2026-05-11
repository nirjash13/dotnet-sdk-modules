using System;
using Ai.Application.Abstractions;
using Ai.Application.Evaluation;
using Ai.Infrastructure.Budgets;
using Ai.Infrastructure.Caching;
using Ai.Infrastructure.Mcp;
using Ai.Infrastructure.Providers;
using Ai.Infrastructure.Rag;
using Ai.Infrastructure.Safety;
using Ai.Infrastructure.VectorStores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ai.Infrastructure.Extensions;

/// <summary>
/// Registers all AI module infrastructure services.
/// Called from <c>AiModule.ConfigureServices</c>.
/// </summary>
public static class AiInfrastructureExtensions
{
    /// <summary>Adds AI infrastructure to the service collection.</summary>
    public static IServiceCollection AddAiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext
        string? connectionString = configuration.GetConnectionString("AiDb")
            ?? configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AiDbContext>(options =>
            options.UseNpgsql(connectionString ?? "Host=localhost;Database=saasbuilder_ai"));

        // Safety
        services.AddSingleton<IPromptSafetyFilter, RegexPiiRedactor>();

        // Caching
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        // MCP
        services.AddSingleton<McpServerEndpoint>();

        // Evaluation
        services.AddScoped<IPromptEvaluator, PromptEvaluator>();

        // Budget tracker
        services.AddScoped<ILlmBudgetTracker, EfCoreLlmBudgetTracker>();

        // Providers
        RegisterLlmClient(services, configuration);
        RegisterEmbeddingClient(services, configuration);
        RegisterVectorStore(services, configuration);

        // RAG pipeline
        services.AddScoped<IRagPipeline, RagPipeline>();

        return services;
    }

    private static void RegisterLlmClient(IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration["Ai:Provider"] ?? "NoOp";

        switch (provider.ToLowerInvariant())
        {
            case "openai":
                services.Configure<OpenAiOptions>(configuration.GetSection("Ai:OpenAi"));
                services.AddHttpClient<OpenAiLlmClient>((sp, client) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiOptions>>().Value;
                    client.BaseAddress = new Uri(opts.Endpoint);
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
                });
                services.AddScoped<ILlmClient>(sp =>
                {
                    var inner = sp.GetRequiredService<OpenAiLlmClient>();
                    return new PromptOutputCache(
                        inner,
                        sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                        sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
                        sp.GetRequiredService<ILogger<PromptOutputCache>>());
                });
                break;

            case "anthropic":
                services.Configure<AnthropicOptions>(configuration.GetSection("Ai:Anthropic"));
                services.AddHttpClient<AnthropicLlmClient>((sp, client) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
                    client.BaseAddress = new Uri("https://api.anthropic.com/v1/");
                    client.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
                    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                });
                services.AddScoped<ILlmClient>(sp =>
                {
                    var inner = sp.GetRequiredService<AnthropicLlmClient>();
                    return new PromptOutputCache(
                        inner,
                        sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                        sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
                        sp.GetRequiredService<ILogger<PromptOutputCache>>());
                });
                break;

            case "ollama":
                services.Configure<OllamaOptions>(configuration.GetSection("Ai:Ollama"));
                services.AddHttpClient<OllamaLlmClient>((sp, client) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
                    client.BaseAddress = new Uri(opts.Endpoint);
                });
                services.AddScoped<ILlmClient, OllamaLlmClient>();
                break;

            default:
                // Silent degradation — no API key configured.
                services.AddScoped<ILlmClient, NoOpLlmClient>();
                break;
        }
    }

    private static void RegisterEmbeddingClient(IServiceCollection services, IConfiguration configuration)
    {
        // GAP: Dedicated embedding model configuration (e.g. text-embedding-3-small) is deferred.
        // For now, share the same provider selection; for OpenAI the LLM client also serves embeddings.
        services.AddScoped<IEmbeddingClient, NoOpEmbeddingClient>();
    }

    private static void RegisterVectorStore(IServiceCollection services, IConfiguration configuration)
    {
        string vectorStore = configuration["Ai:VectorStore"] ?? "PgVector";

        switch (vectorStore.ToLowerInvariant())
        {
            case "qdrant":
                services.Configure<QdrantOptions>(configuration.GetSection("Ai:Qdrant"));
                services.AddHttpClient<QdrantVectorStore>((sp, client) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantOptions>>().Value;
                    client.BaseAddress = new Uri(opts.Endpoint);
                    if (!string.IsNullOrEmpty(opts.ApiKey))
                    {
                        client.DefaultRequestHeaders.Add("api-key", opts.ApiKey);
                    }
                });
                services.AddScoped<IVectorStore, QdrantVectorStore>();
                break;

            default: // pgvector
                services.AddScoped<IVectorStore, PgVectorStore>();
                break;
        }
    }
}
