using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SaasBuilder.HostedUi.Areas.Identity.Pages;

/// <summary>Accept an invitation token and create an account — no authentication required.</summary>
[AllowAnonymous]
public sealed class AcceptInvitationModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AcceptInvitationModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>The invitation token read from query string.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? OrganizationName { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool IsExpired { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            IsExpired = true;
            return Page();
        }

        // Pre-validate the token to show org name and detect expiry.
        var client = _httpClientFactory.CreateClient("SaasBuilderApi");
        using var response = await client
            .GetAsync($"/api/v1/identity/invitations/{Uri.EscapeDataString(Token)}", ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            IsExpired = true;
            return Page();
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false),
            cancellationToken: ct).ConfigureAwait(false);

        OrganizationName = doc.RootElement.TryGetProperty("organizationName", out var orgEl)
            ? orgEl.GetString()
            : "your organization";

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            IsExpired = true;
            return Page();
        }

        var client = _httpClientFactory.CreateClient("SaasBuilderApi");

        var payload = JsonSerializer.Serialize(new
        {
            token = Token,
            name = Input.Name,
            password = Input.Password,
        });

        using var content = new System.Net.Http.StringContent(
            payload, System.Text.Encoding.UTF8, "application/json");

        using var response = await client
            .PostAsync("/api/v1/identity/invitations/accept", content, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Gone ||
                response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                IsExpired = true;
            }
            else
            {
                ErrorMessage = "Could not accept invitation. Please try again.";
            }

            return Page();
        }

        // Invitation accepted — redirect to login.
        return RedirectToPage("Login", new { ReturnUrl = "/" });
    }

    /// <summary>Input model for the account-creation form.</summary>
    public sealed class InputModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
