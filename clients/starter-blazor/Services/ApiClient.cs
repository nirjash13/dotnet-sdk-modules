using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SaasBuilder.Starter.Blazor.Services;

/// <summary>
/// Central HTTP client for the SaasBuilder API.
/// Attaches Bearer tokens, handles 401 token refresh, and exposes typed request helpers.
/// Register as scoped via <c>builder.Services.AddScoped&lt;ApiClient&gt;()</c>.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private string? _refreshToken;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public string? AccessToken => _accessToken;

    public void SetTokens(string accessToken, string? refreshToken = null)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public void ClearTokens()
    {
        _accessToken = null;
        _refreshToken = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(path, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized && _refreshToken is not null)
        {
            await RefreshAccessTokenAsync(ct).ConfigureAwait(false);
            response = await _http.GetAsync(path, ct).ConfigureAwait(false);
        }

        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var response = await SendWithRefreshAsync(
            () => _http.PostAsJsonAsync(path, body, ct), path, body, isPost: true, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task PostAsync(string path, object body, CancellationToken ct = default)
    {
        using var response = await SendWithRefreshAsync(
            () => _http.PostAsJsonAsync(path, body, ct), path, body, isPost: true, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(path, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized && _refreshToken is not null)
        {
            await RefreshAccessTokenAsync(ct).ConfigureAwait(false);
            response = await _http.DeleteAsync(path, ct).ConfigureAwait(false);
        }

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Authenticates with email + password via OAuth2 password grant.
    /// Returns <see cref="LoginResult.MfaRequired"/> when the server responds with
    /// HTTP 401 and a <c>mfa_required</c> error code.
    /// </summary>
    public async Task<LoginResult> LoginAsync(
        string email, string password, CancellationToken ct = default)
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

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            using var errDoc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            if (errDoc.RootElement.TryGetProperty("error", out var errProp)
                && errProp.GetString() == "mfa_required")
            {
                return LoginResult.MfaRequired;
            }

            return LoginResult.Failure;
        }

        if (!response.IsSuccessStatusCode)
        {
            return LoginResult.Failure;
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (accessToken is null)
        {
            return LoginResult.Failure;
        }

        SetTokens(accessToken, refreshToken);
        return LoginResult.Success;
    }

    /// <summary>
    /// Submits an MFA TOTP code to complete login after <see cref="LoginResult.MfaRequired"/>.
    /// </summary>
    public async Task<bool> VerifyMfaAsync(
        string email, string totpCode, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(
            "/api/v1/identity/mfa/verify",
            new { Email = email, Code = totpCode },
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt)
            ? rt.GetString()
            : null;

        if (accessToken is null)
        {
            return false;
        }

        SetTokens(accessToken, refreshToken);
        return true;
    }

    private async Task RefreshAccessTokenAsync(CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = _refreshToken!,
        };

        using var response = await _http
            .PostAsync("/connect/token", new FormUrlEncodedContent(form), ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ClearTokens();
            return;
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (accessToken is not null)
        {
            SetTokens(accessToken, refreshToken);
        }
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(
        Func<Task<HttpResponseMessage>> send,
        string path,
        object body,
        bool isPost,
        CancellationToken ct)
    {
        var response = await send().ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized && _refreshToken is not null)
        {
            await RefreshAccessTokenAsync(ct).ConfigureAwait(false);
            response.Dispose();
            response = await send().ConfigureAwait(false);
        }

        return response;
    }
}

public enum LoginResult
{
    Success,
    MfaRequired,
    Failure,
}
