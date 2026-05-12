// NOTE: Handler unit test — lives here because no dedicated unit-test project exists for this module.
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Abstractions;
using Notifications.Infrastructure.Extensions;
using Xunit;

namespace SaasBuilder.IntegrationTests.Notifications;

/// <summary>
/// Load-bearing DI registration test for M-O1.
///
/// The bug: <c>TwilioSmsDispatcher</c> was registered as itself but NOT as
/// <see cref="INotificationDispatcher"/>. When the notification pipeline iterated
/// all <see cref="INotificationDispatcher"/> registrations, SMS messages were silently
/// dropped because no dispatcher claimed the SMS channel.
///
/// Failure signal: if the M-O1 fix (registering TwilioSmsDispatcher as INotificationDispatcher)
/// is reverted, <see cref="IEnumerable{INotificationDispatcher}"/> resolved from DI will not
/// contain any TwilioSmsDispatcher instance, and this test will fail.
/// </summary>
public sealed class SmsDispatcherRegistrationTests
{
    private const string ValidAccountSid = "ACtest000000000000000000000000001";
    private const string ValidAuthToken = "auth_token_value_for_tests_only_ok";

    [Fact]
    public void AddNotificationsInfrastructure_WhenTwilioConfigPresent_RegistersTwilioSmsAsINotificationDispatcher()
    {
        // Arrange — build a minimal IConfiguration that has Twilio credentials.
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Twilio:AccountSid"] = ValidAccountSid,
                ["Notifications:Twilio:AuthToken"] = ValidAuthToken,
                ["Notifications:Twilio:FromNumber"] = "+15005550006",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationsInfrastructure(config);

        ServiceProvider provider = services.BuildServiceProvider();

        // Act — resolve all INotificationDispatcher registrations.
        IEnumerable<INotificationDispatcher> dispatchers =
            provider.GetServices<INotificationDispatcher>().ToList();

        // Assert — at least one registration must be TwilioSmsDispatcher.
        // (TwilioSmsDispatcher is internal; compare by type name to avoid a cross-assembly
        // accessibility issue while still asserting the correct concrete type is present.)
        dispatchers.Should().Contain(
            d => d.GetType().Name == "TwilioSmsDispatcher",
            because: "when Twilio is configured, TwilioSmsDispatcher must be reachable " +
                     "as INotificationDispatcher so the SMS channel is not silently dropped");
    }
}

