using MudBlazor;
using Microsoft.JSInterop;

namespace SaasBuilder.Starter.Blazor.Services;

/// <summary>
/// Loads tenant branding from <c>/api/v1/branding</c> and applies
/// primary/secondary colours as both a <see cref="MudTheme"/> and CSS custom properties.
/// </summary>
public sealed class BrandingService
{
    private readonly ApiClient _apiClient;
    private readonly IJSRuntime _js;
    private BrandingDto? _branding;

    public BrandingService(ApiClient apiClient, IJSRuntime js)
    {
        _apiClient = apiClient;
        _js = js;
    }

    /// <summary>Returns the current theme, refreshing from the API if not yet loaded.</summary>
    public async Task<MudTheme> GetThemeAsync(CancellationToken ct = default)
    {
        _branding ??= await _apiClient.GetAsync<BrandingDto>("/api/v1/branding", ct)
                        .ConfigureAwait(false);

        return BuildTheme(_branding);
    }

    /// <summary>
    /// Injects CSS custom properties into <c>document.documentElement</c>
    /// so that non-MudBlazor elements can consume brand colours.
    /// </summary>
    public async Task ApplyCssVarsAsync(CancellationToken ct = default)
    {
        _branding ??= await _apiClient.GetAsync<BrandingDto>("/api/v1/branding", ct)
                        .ConfigureAwait(false);

        if (_branding is null)
        {
            return;
        }

        // C-26 FIX: Use structured JS interop instead of eval().
        // The JS helper (window.saasBuilderBranding.setCssVar) validates the color value
        // against a regex allowlist (#RGB / #RRGGBB / #RRGGBBAA / CSS named colors)
        // before calling document.documentElement.style.setProperty().
        // This prevents XSS via tenant-controlled color strings that contained JS payloads.
        if (!string.IsNullOrWhiteSpace(_branding.PrimaryColor))
        {
            await _js.InvokeVoidAsync(
                "saasBuilderBranding.setCssVar", "--brand-primary", _branding.PrimaryColor)
                .ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(_branding.SecondaryColor))
        {
            await _js.InvokeVoidAsync(
                "saasBuilderBranding.setCssVar", "--brand-secondary", _branding.SecondaryColor)
                .ConfigureAwait(false);
        }
    }

    private static MudTheme BuildTheme(BrandingDto? branding)
    {
        var palette = new PaletteLight();

        if (!string.IsNullOrWhiteSpace(branding?.PrimaryColor))
        {
            palette.Primary = branding.PrimaryColor;
        }

        if (!string.IsNullOrWhiteSpace(branding?.SecondaryColor))
        {
            palette.Secondary = branding.SecondaryColor;
        }

        return new MudTheme { PaletteLight = palette };
    }

    private sealed record BrandingDto(
        string? PrimaryColor,
        string? SecondaryColor,
        string? LogoUrl,
        string? AppName);
}
