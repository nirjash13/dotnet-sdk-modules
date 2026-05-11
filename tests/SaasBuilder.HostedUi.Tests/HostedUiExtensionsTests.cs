using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SaasBuilder.HostedUi;
using Xunit;

namespace SaasBuilder.HostedUi.Tests;

public sealed class HostedUiExtensionsTests
{
    /// <summary>
    /// Load-bearing test 1: AddSaasBuilderHostedUi registers Razor Pages services
    /// so that RazorPagesOptions is available in the DI container.
    /// </summary>
    [Fact]
    public void AddSaasBuilderHostedUi_RegistersRazorPagesServices()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Act
        builder.AddSaasBuilderHostedUi();

        using var app = builder.Build();

        // Assert — RazorPagesOptions is registered when AddRazorPages is called.
        var razorPagesOptions = app.Services.GetService<Microsoft.Extensions.Options.IOptions<RazorPagesOptions>>();
        razorPagesOptions.Should().NotBeNull("AddRazorPages must be called by AddSaasBuilderHostedUi");
    }

    /// <summary>
    /// Load-bearing test 2: AddSaasBuilderHostedUi registers the HostedUi assembly as
    /// an application part so the area pages are discoverable by the Razor runtime.
    /// </summary>
    [Fact]
    public void AddSaasBuilderHostedUi_RegistersHostedUiAssemblyPart()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Act
        builder.AddSaasBuilderHostedUi();
        using var app = builder.Build();

        // Assert
        var apm = app.Services.GetRequiredService<ApplicationPartManager>();
        var hostedUiAssemblyName = typeof(SaasBuilderHostedUiExtensions).Assembly.GetName().Name;

        apm.ApplicationParts
            .Should()
            .Contain(
                p => p.Name == hostedUiAssemblyName,
                $"the HostedUi assembly '{hostedUiAssemblyName}' must be registered as an application part");
    }

    /// <summary>
    /// Load-bearing test 3: AcceptInvitation page model sets IsExpired=true when
    /// no token is present in the query string — verifying the guard path that prevents
    /// a null-token from being sent to the API.
    /// </summary>
    [Fact]
    public async Task AcceptInvitation_WhenTokenIsMissing_SetsIsExpiredTrue()
    {
        // Arrange — minimal HttpClientFactory stub; the page hits IsExpired before calling the API.
        var services = new ServiceCollection();
        services.AddHttpClient("SaasBuilderApi", c =>
            c.BaseAddress = new Uri("http://localhost"));

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var model = new SaasBuilder.HostedUi.Areas.Identity.Pages.AcceptInvitationModel(factory)
        {
            // Token intentionally NOT set — simulates no query-string token
            Token = null,
        };

        // Act — simulate the Razor Pages GET handler directly (no HTTP stack needed)
        await model.OnGetAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — IsExpired must be true so the view shows the "expired" message
        model.IsExpired.Should().BeTrue(
            "when Token is null the page must flag the invitation as expired/invalid");
    }
}
