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

// Register the SaasBuilder API client
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddScoped<SaasBuilderClientService>();

// MudBlazor for Material Design components
builder.Services.AddMudServices();

await builder.Build().RunAsync().ConfigureAwait(false);
