using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace SaasBuilder.HostedUi.Areas.Billing.Pages;

/// <summary>
/// Fetches a customer portal session URL from the API and 302-redirects the user.
/// Requires authentication.
/// </summary>
[Authorize]
public sealed class PortalRedirectModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public PortalRedirectModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var accessToken = Request.Cookies["sb_token"];
        if (string.IsNullOrEmpty(accessToken))
        {
            return RedirectToPage("/Identity/Login", new { ReturnUrl = "/Billing/PortalRedirect" });
        }

        var client = _httpClientFactory.CreateClient("SaasBuilderApi");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var content = new System.Net.Http.StringContent(
            "{}", System.Text.Encoding.UTF8, "application/json");

        using var response = await client
            .PostAsync("/api/v1/billing/customer-portal/session", content, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Could not open the billing portal. Please try again.";
            return Page();
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        var portalUrl = doc.RootElement.GetProperty("url").GetString();

        if (string.IsNullOrEmpty(portalUrl))
        {
            ErrorMessage = "Billing portal URL was empty. Please contact support.";
            return Page();
        }

        return Redirect(portalUrl);
    }
}
