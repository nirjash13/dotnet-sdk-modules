using System;
using System.Collections.Generic;
using System.Security.Claims;
using Identity.Application.ApiKeys;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Identity.Api.ApiKeys;

/// <summary>
/// Minimal API endpoints for API key management (Phase 2).
/// All endpoints require authentication; the raw key is returned ONLY at creation/rotation.
/// </summary>
public static class ApiKeyEndpoints
{
    /// <summary>Maps API key CRUD and rotation endpoints.</summary>
    public static void MapApiKeyEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder keys = endpoints
            .MapGroup("/api/v1/identity/api-keys")
            .RequireAuthorization()
            .WithTags("identity-api-keys");

        keys.MapPost(string.Empty, CreateApiKey)
            .WithName("Identity_ApiKeys_Create")
            .WithSummary("Creates a new API key. The raw key is returned ONCE and must be stored securely.");

        keys.MapGet(string.Empty, ListApiKeys)
            .WithName("Identity_ApiKeys_List")
            .WithSummary("Lists active API keys for the authenticated user.");

        keys.MapDelete("{id:guid}", RevokeApiKey)
            .WithName("Identity_ApiKeys_Revoke")
            .WithSummary("Revokes an API key, preventing future use.");

        keys.MapPost("{id:guid}/rotate", RotateApiKey)
            .WithName("Identity_ApiKeys_Rotate")
            .WithSummary("Rotates an API key: revokes the old one and creates a new one. The new raw key is returned ONCE.");
    }

    private static async Task<IResult> CreateApiKey(
        CreateApiKeyRequest request,
        ClaimsPrincipal user,
        IApiKeyService apiKeyService,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out Guid userId))
        {
            return Results.Unauthorized();
        }

        IEnumerable<string> scopes = request.Scopes ?? Array.Empty<string>();
        ApiKeyCreatedResult result = await apiKeyService.CreateAsync(userId, scopes, ct);

        return Results.Created(
            $"/api/v1/identity/api-keys/{result.KeyId}",
            new
            {
                keyId = result.KeyId,
                rawKey = result.RawKey,
                warning = "Store this key securely — it will not be shown again.",
            });
    }

    private static async Task<IResult> ListApiKeys(
        ClaimsPrincipal user,
        IApiKeyStore store,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out Guid userId))
        {
            return Results.Unauthorized();
        }

        IReadOnlyList<Identity.Domain.Entities.ApiKey> keys = await store.ListByUserIdAsync(userId, ct);

        return Results.Ok(keys.Select(k => new
        {
            id = k.Id,
            name = k.Name,
            keyPrefix = k.KeyPrefix,
            createdAt = k.CreatedAt,
            lastUsedAt = k.LastUsedAt,
        }));
    }

    private static async Task<IResult> RevokeApiKey(
        Guid id,
        ClaimsPrincipal user,
        IApiKeyService apiKeyService,
        IApiKeyStore store,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out Guid userId))
        {
            return Results.Unauthorized();
        }

        Identity.Domain.Entities.ApiKey? key = await store.FindByIdAsync(id, ct);
        if (key is null || key.UserId != userId)
        {
            return Results.NotFound(new { code = "NOT_FOUND", detail = "API key not found." });
        }

        await apiKeyService.RevokeAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RotateApiKey(
        Guid id,
        ClaimsPrincipal user,
        IApiKeyService apiKeyService,
        IApiKeyStore store,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out Guid userId))
        {
            return Results.Unauthorized();
        }

        Identity.Domain.Entities.ApiKey? key = await store.FindByIdAsync(id, ct);
        if (key is null || key.UserId != userId)
        {
            return Results.NotFound(new { code = "NOT_FOUND", detail = "API key not found." });
        }

        ApiKeyCreatedResult result = await apiKeyService.RotateAsync(id, ct);
        return Results.Ok(new
        {
            newKeyId = result.KeyId,
            rawKey = result.RawKey,
            warning = "Store this key securely — it will not be shown again.",
        });
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        string? sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    /// <summary>Request body for API key creation.</summary>
    public sealed record CreateApiKeyRequest(IReadOnlyList<string>? Scopes);
}
