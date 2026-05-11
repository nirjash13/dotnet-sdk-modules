using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SaasBuilder.HostedUi.Areas.Identity.Pages;

/// <summary>
/// MFA setup + verification page.
/// Works for two scenarios:
/// 1. New TOTP enrollment — loads QR code from /api/v1/identity/mfa/totp/enroll.
/// 2. Step-up verification after login — uses mfaToken from query string.
/// </summary>
[Authorize]
public sealed class MfaSetupModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MfaSetupModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty(SupportsGet = true)]
    public string? MfaToken { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter a 6-digit code.")]
    public string Code { get; set; } = string.Empty;

    public string? QrCodeUri { get; private set; }

    public string? ManualKey { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // If a mfaToken is present this is a step-up login — no QR code needed.
        if (!string.IsNullOrEmpty(MfaToken))
        {
            return Page();
        }

        // Otherwise load enrollment QR from the API.
        var accessToken = Request.Cookies["sb_token"];
        if (string.IsNullOrEmpty(accessToken))
        {
            return RedirectToPage("Login", new { ReturnUrl = "/Identity/MfaSetup" });
        }

        var client = _httpClientFactory.CreateClient("SaasBuilderApi");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client
            .GetAsync("/api/v1/identity/mfa/totp/enroll", ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Could not load MFA enrollment. Please try again.";
            return Page();
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        QrCodeUri = doc.RootElement.GetProperty("qrCodeUri").GetString();
        ManualKey = doc.RootElement.GetProperty("manualKey").GetString();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = _httpClientFactory.CreateClient("SaasBuilderApi");

        string verifyPath;
        object body;

        if (!string.IsNullOrEmpty(MfaToken))
        {
            // Step-up after login — uses the temporary mfa_token
            verifyPath = "/api/v1/identity/mfa/verify";
            body = new { code = Code, mfa_token = MfaToken };
        }
        else
        {
            // Enrollment verification
            var accessToken = Request.Cookies["sb_token"];
            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToPage("Login");
            }

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            verifyPath = "/api/v1/identity/mfa/totp/confirm";
            body = new { code = Code };
        }

        var json = JsonSerializer.Serialize(body);
        using var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(verifyPath, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Invalid code. Please try again.";
            return Page();
        }

        // For step-up, the response contains new tokens — store them.
        if (!string.IsNullOrEmpty(MfaToken))
        {
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
                cancellationToken: ct).ConfigureAwait(false);

            var newToken = doc.RootElement.GetProperty("access_token").GetString();
            if (!string.IsNullOrEmpty(newToken))
            {
                Response.Cookies.Append("sb_token", newToken, new CookieOptions
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
}
