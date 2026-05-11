using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SaasBuilder.SharedKernel.Tenancy;

namespace Ai.Infrastructure.Mcp;

/// <summary>
/// Stub implementation of the Model Context Protocol (MCP) server endpoint.
/// Exposes tenant-scoped tools via JSON-RPC 2.0 at <c>POST /api/v1/ai/mcp</c>.
/// <para>
/// GAP: The full MCP wire-protocol (initialize, list_tools, call_tool) requires
/// bidirectional SSE or WebSocket transport. This stub handles the JSON-RPC envelope
/// and returns the tool registry for the current tenant. Full wire-protocol
/// transport is a Phase 10.x enhancement.
/// </para>
/// </summary>
public sealed class McpServerEndpoint
{
    private readonly IEnumerable<IMcpTool> _tools;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ILogger<McpServerEndpoint> _logger;

    /// <summary>Initializes a new instance of <see cref="McpServerEndpoint"/>.</summary>
    public McpServerEndpoint(
        IEnumerable<IMcpTool> tools,
        ITenantContextAccessor tenantAccessor,
        ILogger<McpServerEndpoint> logger)
    {
        _tools = tools;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Handles a JSON-RPC 2.0 request body and returns a JSON-RPC response object.
    /// Supported methods: <c>initialize</c>, <c>tools/list</c>.
    /// </summary>
    public async Task<object> HandleAsync(JsonElement request, CancellationToken ct = default)
    {
        ITenantContext? tenant = _tenantAccessor.Current;
        if (tenant is null)
        {
            return JsonRpcError(request, -32001, "Tenant context not available.");
        }

        string? method = request.TryGetProperty("method", out JsonElement methodEl)
            ? methodEl.GetString()
            : null;

        string? id = request.TryGetProperty("id", out JsonElement idEl)
            ? idEl.ToString()
            : null;

        _logger.LogDebug("MCP request: method={Method}, tenantId={TenantId}", method, tenant.TenantId);

        await Task.CompletedTask.ConfigureAwait(false);

        return method switch
        {
            "initialize" => new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = "SaasBuilder MCP", version = "1.0.0" },
                },
            },
            "tools/list" => new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    tools = _tools
                        .Where(t => t.IsAvailableForTenant(tenant.TenantId))
                        .Select(t => new { name = t.Name, description = t.Description, inputSchema = t.InputSchema })
                        .ToList(),
                },
            },
            _ => JsonRpcError(request, -32601, $"Method '{method}' not found."),
        };
    }

    private static object JsonRpcError(JsonElement request, int code, string message)
    {
        string? id = request.TryGetProperty("id", out JsonElement idEl) ? idEl.ToString() : null;
        return new
        {
            jsonrpc = "2.0",
            id,
            error = new { code, message },
        };
    }
}

/// <summary>Contract for a tool exposed via the MCP server.</summary>
public interface IMcpTool
{
    /// <summary>Gets the tool name (snake_case).</summary>
    string Name { get; }

    /// <summary>Gets the human-readable tool description.</summary>
    string Description { get; }

    /// <summary>Gets the JSON Schema object describing the tool's input parameters.</summary>
    object InputSchema { get; }

    /// <summary>Returns true if this tool is available for the given tenant.</summary>
    bool IsAvailableForTenant(Guid tenantId);

    /// <summary>Executes the tool with the given arguments JSON.</summary>
    Task<object> ExecuteAsync(JsonElement arguments, Guid tenantId, CancellationToken ct = default);
}
