using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SaasBuilder.Starter.Blazor.Services;

/// <summary>
/// Client service wrapping <see cref="HttpClient"/> with automatic Bearer token injection.
/// Register via <c>builder.Services.AddScoped&lt;SaasBuilderClientService&gt;()</c>.
/// </summary>
public sealed class SaasBuilderClientService
{
    private readonly HttpClient _http;
    private string? _accessToken;

    public SaasBuilderClientService(HttpClient http)
    {
        _http = http;
    }

    public string? AccessToken => _accessToken;

    public void SetTokens(string accessToken, string? refreshToken = null)
    {
        _accessToken = accessToken;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        // Refresh token stored in browser session storage by caller.
        _ = refreshToken; // consumed by caller via JS interop
    }

    public void ClearTokens()
    {
        _accessToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
        => _http.GetFromJsonAsync<T>(path, ct);

    public async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task PostAsync(string path, object body, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(path, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync(path, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Exchanges username + password for tokens via the OAuth2 password grant.
    /// Returns true on success; populates <see cref="AccessToken"/>.
    /// </summary>
    public async Task<bool> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = email,
            ["password"] = password,
            ["scope"] = "openid offline_access",
        };

        using var response = await _http
            .PostAsync("/connect/token", new FormUrlEncodedContent(form), ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (accessToken is null)
        {
            return false;
        }

        SetTokens(accessToken, refreshToken);
        return true;
    }
}
