using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SaasBuilder.HostedUi.Areas.Identity.Pages;

/// <summary>Login page — no authentication required.</summary>
[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public LoginModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
        // Nothing to initialize; ReturnUrl is bound from query string.
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = _httpClientFactory.CreateClient("SaasBuilderApi");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = Input.Email,
            ["password"] = Input.Password,
            ["scope"] = "openid offline_access",
        });

        using var response = await client.PostAsync("/connect/token", form, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        var root = doc.RootElement;

        // Handle MFA challenge
        if (root.TryGetProperty("requires_mfa", out var mfaEl) && mfaEl.GetBoolean())
        {
            var mfaToken = root.GetProperty("mfa_token").GetString() ?? string.Empty;
            return RedirectToPage("MfaSetup", new { mfaToken, ReturnUrl });
        }

        var accessToken = root.GetProperty("access_token").GetString();
        if (string.IsNullOrEmpty(accessToken))
        {
            ErrorMessage = "Authentication failed.";
            return Page();
        }

        // Store token in a secure HttpOnly cookie.
        Response.Cookies.Append("sb_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });

        if (root.TryGetProperty("refresh_token", out var rtEl))
        {
            var refreshToken = rtEl.GetString();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                Response.Cookies.Append("sb_refresh", refreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                });
            }
        }

        var destination = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        return LocalRedirect(destination);
    }

    /// <summary>Input model for the login form.</summary>
    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
