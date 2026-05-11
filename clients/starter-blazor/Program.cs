using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using SaasBuilder.Starter.Blazor;
using SaasBuilder.Starter.Blazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Base URL is read from wwwroot/appsettings.json at runtime.
// Key: "ApiBaseUrl" — e.g. "https://api.acme.com"
var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
    ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Core API client with Bearer token management + 401 refresh
builder.Services.AddScoped<ApiClient>();

// Notification hub (SignalR to /hubs/notifications)
builder.Services.AddScoped<NotificationsHubClient>();

// Branding: reads /api/v1/branding and applies MudTheme + CSS vars
builder.Services.AddScoped<BrandingService>();

// MudBlazor Material Design components
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
});

await builder.Build().RunAsync().ConfigureAwait(false);
