using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SaasBuilder.HostedUi;
using Xunit;

namespace SaasBuilder.HostedUi.Tests;

public sealed class HostedUiExtensionsTests
{
    /// <summary>
    /// Load-bearing: AcceptInvitation page model sets IsExpired=true when no token is present
    /// in the query string — verifying the guard path that prevents a null-token from being
    /// sent to the API.
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
